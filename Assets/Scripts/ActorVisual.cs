using UnityEngine;

namespace Krypta
{
    public sealed class ActorVisual
    {
        readonly Animation animation;
        readonly bool fitActionDuration;
        readonly float actorHeight, walkStridePerHeight, runStridePerHeight;
        Vector3 previousPosition;
        float previousTick, locomotionSpeed;
        GameObject equippedWeapon;
        public int ActionSequence { get; private set; }
        public float LocomotionSpeed { get { return locomotionSpeed; } }
        string current;
        float lockedUntil, movementReleaseAt;
        bool recoverIntoLocomotion;
        public bool MovementCommitted { get { return recoverIntoLocomotion && Time.time < movementReleaseAt && Time.time < lockedUntil; } }
        public readonly GameObject Root;
        public float CurrentClipNormalizedTime { get { return animation && !string.IsNullOrEmpty(current) && animation[current] ? animation[current].normalizedTime : 0; } }
        public string CurrentClip { get { return current; } }
        public float CurrentClipTime { get { return animation && !string.IsNullOrEmpty(current) && animation[current] ? animation[current].time : 0; } }

        public ActorVisual(GameObject prefab, AnimationClip[] clips, Vector3 position, float height, string name)
        {
            Root = new GameObject(name);
            Root.transform.position = position;
            actorHeight=height;previousPosition=position;previousTick=Time.time;
            GameObject model = Object.Instantiate(prefab, Root.transform);
            ModelSurface.Apply(model);
            model.transform.localPosition = Vector3.zero;
            // KayKit embeds alternative weapons together; equip only the chosen weapon.
            foreach(Transform slot in model.GetComponentsInChildren<Transform>())
                if(slot.name=="handslot.r" || slot.name=="handslot.l")
                    foreach(Transform option in slot)
                        option.gameObject.SetActive(option.name=="Badge_Shield");
            Animator[] animators = model.GetComponentsInChildren<Animator>();
            foreach (Animator a in animators) a.enabled = false;
            animation = model.GetComponentInChildren<Animation>();
            // glTF animation paths are relative to the imported Animator's object.
            if (!animation) animation = (animators.Length > 0 ? animators[0].gameObject : model).AddComponent<Animation>();
            animation.playAutomatically = false;
            animation.cullingType = AnimationCullingType.AlwaysAnimate;
            if (clips != null)
                foreach (AnimationClip clip in clips)
                    if (clip) animation.AddClip(clip, clip.name);
            Bounds bounds;
            var profile=model.GetComponent<ActorModelProfile>();
            fitActionDuration=profile && profile.fitActionDuration;
            walkStridePerHeight=SafeStride(profile ? profile.walkStridePerHeight : .60f,.60f);
            runStridePerHeight=SafeStride(profile ? profile.runStridePerHeight : 1.45f,1.45f);
            if(profile)
            {
                float scale=height/Mathf.Max(.01f,profile.bodyHeight);
                model.transform.localScale*=scale;
                model.transform.localPosition=Vector3.up*(profile.groundOffset*scale);
            }
            else if (ModelBounds.TryGet(model,out bounds))
            {
                float scale = height / Mathf.Max(.01f, bounds.size.y);
                model.transform.localScale *= scale;
                model.transform.localPosition = new Vector3(0, (position.y - bounds.min.y) * scale, 0);
            }
            Play("Idle", true, 0);
            // Title screen pauses game time: explicitly sample a resting pose for portraits.
            if(animation && animation["Idle"])
            { animation.Play("Idle"); animation["Idle"].time=.15f; animation.Sample(); }
        }

        public void Tick(bool moving)
        {
            if(Time.deltaTime<=0)return;
            // Cadence follows distance actually travelled. Walking into a wall must
            // stop the feet, and slow effects must slow the cycle with the actor.
            Vector3 delta=Root.transform.position-previousPosition;delta.y=0;
            float elapsed=Time.time-previousTick;
            previousPosition=Root.transform.position;previousTick=Time.time;
            float speed=elapsed>.0001f && elapsed<1f && delta.magnitude<actorHeight*2
                ? delta.magnitude/elapsed : 0;
            locomotionSpeed=moving ? speed : 0;
            if (Time.time < lockedUntil) return;
            if(!animation)return;
            float runThreshold=current=="Running_A" ? 2.35f : 2.55f;
            string clip=locomotionSpeed<.05f ? "Idle" : locomotionSpeed<runThreshold && animation["Walking_A"] ? "Walking_A" : "Running_A";
            Play(clip,true,0);
            if(clip!="Idle" && animation && animation[clip])
            {
                float stride=actorHeight*(clip=="Walking_A" ? walkStridePerHeight : runStridePerHeight);
                animation[clip].speed=Mathf.Clamp(locomotionSpeed*animation[clip].length/stride,.35f,2.8f);
            }
        }

        // Full-body actions plant the feet through contact, then movement may
        // cancel their visual recovery. Damage/cooldowns keep their own clocks.
        public void PrepareLocomotion(bool requested)
        {
            if(requested && recoverIntoLocomotion && Time.time>=movementReleaseAt)
                lockedUntil=Mathf.Min(lockedUntil,Time.time);
        }

        static float SafeStride(float value,float fallback)
        { return float.IsNaN(value) || float.IsInfinity(value) || value<=.01f ? fallback : value; }

        // Optional cosmetic gesture. It cannot replace a protected action and
        // yields to movement immediately; interacting never adds a movement lock.
        public bool TryPlayInteraction()
        {
            if(!animation || !animation["Interact_A"] || Time.time<lockedUntil)return false;
            Play("Interact_A",false,.45f);
            movementReleaseAt=Time.time;
            return true;
        }

        public void PreviewIdle(float time)
        {
            if(!animation || !animation["Idle"])return;
            // Character selection pauses scaled time. CrossFade never advances
            // there, so explicitly activate one full-weight pose before sampling.
            animation.Play("Idle",PlayMode.StopAll);
            var idle=animation["Idle"];idle.enabled=true;idle.weight=1;idle.speed=0;
            idle.time=Mathf.Repeat(time,Mathf.Max(.01f,idle.length));
            animation.Sample();current="Idle";
            var grip=equippedWeapon?equippedWeapon.GetComponent<NocturnisWeaponGrip>():null;if(grip)grip.Apply();
        }

        public void Play(string clip, bool loop, float lockSeconds)
        {
            if (!animation || !animation[clip]) return;
            if (current == clip && loop) return;
            float gaitPhase=0;
            bool preserveGaitPhase=loop && (clip=="Walking_A" || clip=="Running_A") && (current=="Walking_A" || current=="Running_A") && animation[current];
            if(preserveGaitPhase)gaitPhase=Mathf.Repeat(animation[current].normalizedTime,1);
            var state=animation[clip];
            state.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
            state.speed=fitActionDuration && !loop && lockSeconds>0 && lockSeconds<10
                ? state.length/lockSeconds : 1;
            // Repeated casts must start a fresh action even when the previous clip is clamped.
            if(!loop){state.time=0;ActionSequence++;}
            else if(preserveGaitPhase)state.normalizedTime=gaitPhase;
            recoverIntoLocomotion=!loop && clip!="Death_A" && clip!="Dodge_Forward";
            float commitment=(clip=="Hit_A" || clip=="Defense_A" || clip=="Block") ? .35f : .58f;
            movementReleaseAt=recoverIntoLocomotion ? Time.time+Mathf.Min(.28f,lockSeconds*commitment) : 0;
            float fade=fitActionDuration && !loop ? Mathf.Min(.08f,lockSeconds*.2f) : .12f;
            animation.CrossFade(clip, fade);
            current = clip;
            lockedUntil = Time.time + lockSeconds;
        }

        public void React(string clip,float seconds)
        {
            if(Time.time<lockedUntil)return;
            Play(clip,false,seconds);
        }

        public void AdvancePausedAnimation(float seconds)
        {
            // Death pauses the simulation; finish only this pose on the unscaled visual clock.
            if(!animation || current!="Death_A" || !animation[current])return;
            var state=animation[current];
            float time=Mathf.Min(state.length,state.time+Mathf.Max(0,seconds)*state.speed);
            animation.Play(current,PlayMode.StopAll);
            state.time=time;
            animation.Sample();
            var grip=equippedWeapon?equippedWeapon.GetComponent<NocturnisWeaponGrip>():null;if(grip)grip.Apply();
        }

        public Vector3 HandPosition(bool right=true)
        {
            string name=right ? "handslot.r" : "handslot.l";
            foreach(var t in Root.GetComponentsInChildren<Transform>())if(t.name==name)return t.position;
            return Root.transform.position+Vector3.up*actorHeight*.65f;
        }

        public void Equip(GameObject weapon)
        {
            if (!weapon) return;
            foreach (Transform t in Root.GetComponentsInChildren<Transform>())
            {
                if (t.name != "handslot.r") continue;
                if(equippedWeapon)Object.Destroy(equippedWeapon);
                GameObject item = Object.Instantiate(weapon, t);equippedWeapon=item;
                ModelSurface.Apply(item);
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
                var profile=t.GetComponentInParent<ActorModelProfile>();
                if(profile && t.parent.name=="handR")item.AddComponent<NocturnisWeaponGrip>().Initialize(this,item.transform,profile.bodyHeight);
                var art=Resources.Load<KryptaAssets>("KryptaAssets");
                foreach(var part in item.GetComponentsInChildren<Transform>(true)){
                    part.gameObject.layer=t.gameObject.layer;
                    if(part.name=="TipAnchor" && art && art.effectMaterial)
                        part.gameObject.AddComponent<NocturnisWeaponTrail>().Initialize(this,art.effectMaterial);
                }
                Bounds ignored;
                ModelBounds.TryGet(item,out ignored);
                return;
            }
        }
    }
}
