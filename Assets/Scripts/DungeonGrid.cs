using System;
using System.Collections.Generic;

namespace Krypta
{
    // Engine-independent grid, shared by movement, line of sight and navigation.
    public sealed class DungeonGrid
    {
        public const int Width = 25, Height = 41;
        readonly bool[,] floor = new bool[Width, Height];
        readonly int[,] distance = new int[Width, Height];
        static readonly int[] DX = { 1, -1, 0, 0 };
        static readonly int[] DZ = { 0, 0, 1, -1 };
        public struct Cell
        {
            public int X, Z;
            public Cell(int x, int z) { X = x; Z = z; }
        }

        // These are authored routes, not a rectangular floor with decorative blockers.
        // Coordinates remain stable for checkpoints, quest markers and campaign exits.
        public DungeonGrid(int zone = 2)
        {
            if(zone==Campaign.Town)
            {
                Carve(2,2,21,20);
                Block(2,5,6,8);Block(17,5,21,8);
                Block(2,12,6,16);Block(17,12,21,16);Block(9,16,14,20);
                BuildDistances(11,5);return;
            }
            if(BuildLongRoute(zone)){BuildDistances(5,3);return;}
            if(zone==Campaign.Road)
            {
                Trail(2.0f,12,3,14,8,10,13,12,16,15,21,11,26,12,30,12,37);
                Ellipse(12,7,4.7f,5.6f);Ellipse(11,27,6.2f,5.6f);
                Trail(1.3f,12,13,18,15,18,20,15,21);Ellipse(18,17,2.2f,3.5f);
                Block(8,4,10,6);BuildDistances(12,3);return;
            }
            if(zone==Campaign.Fields)
            {
                Trail(3.3f,5,3,6,7,12,10,14,15,8,20,10,26,17,30,17,38);
                Ellipse(6,5,4.6f,4.5f);Ellipse(9,18,7.2f,6.5f);Ellipse(17,33,6.8f,6.0f);
                Trail(1.7f,7,18,3,20,3,23,7,25,10,26);
                Trail(2.0f,13,14,20,15,21,17);Ellipse(19,15,3.5f,2.8f);
                Trail(1.5f,12,24,17,24,21,28,21,34);
                Block(2,9,8,13);Block(16,18,22,22);Block(2,28,8,31);Block(11,19,12,22);
                EnsureLegacyPins();BuildDistances(5,3);return;
            }
            if(zone==Campaign.Forest)
            {
                Trail(1.7f,5,3,5,8,9,12,5,17,7,22,13,24,12,30,18,33,17,38);
                Ellipse(5,6,4.0f,5.5f);Ellipse(7,19,5.8f,5.0f);Ellipse(17,34,6.0f,5.5f);
                Trail(1.4f,7,18,13,16,16,19,13,24);Ellipse(14,18,3.8f,3.5f);
                Trail(1.2f,7,23,3,25,3,29,9,31,12,30);
                Trail(1.4f,18,33,21,30);EnsureLegacyPins();BuildDistances(5,3);return;
            }
            if(zone==Campaign.Cemetery)
            {
                Trail(1.9f,12,3,12,11,10,16,12,20,12,27,16,32,17,38);
                Trail(1.6f,4,5,4,10,11,10,20,10,20,5);
                Trail(1.6f,4,10,4,22,4,33,7,35,12,32,20,34,20,25,12,25);
                Ellipse(4,5,3.2f,3.1f);Ellipse(20,5,3.1f,3.1f);
                Ellipse(5,33,3.5f,3.3f);Ellipse(20,35,3.0f,3.8f);
                Ellipse(12,21,3.0f,4.8f);Trail(1.4f,12,18,19,16,20,21,20,25);
                Trail(1.2f,12,15,11,15);BuildDistances(12,3);return;
            }
            if(zone==Campaign.Heart)
            {
                Ellipse(12,25,10.5f,10.5f);Carve(11,2,13,16);Carve(11,34,13,38);
                floor[5,18]=floor[19,18]=floor[5,32]=floor[19,32]=false;
                BuildDistances(12,3);return;
            }
            if(zone==Campaign.Cellar)
            {
                // Wine store, collapsed bend, service kitchen, vaulted larder.
                Carve(2,1,8,9);Carve(4,9,6,15);Carve(2,14,13,23);
                Carve(11,20,13,28);Carve(10,27,22,38);Carve(15,37,19,39);
                Carve(17,16,22,22);Carve(13,19,17,21);
                Block(8,14,9,17);Block(15,29,16,32);Block(19,35,20,37);
            }
            else if(zone==Campaign.Chapel)
            {
                // Transepts and a rounded apse, with a loop through the sacristy.
                Ellipse(5,6,4.3f,5.5f);Carve(4,10,6,15);
                Carve(2,15,14,23);Carve(8,23,12,30);Carve(7,27,22,32);
                Ellipse(16,34,7.3f,5.8f);Carve(17,16,22,24);Carve(13,19,18,21);
                Carve(19,23,21,29);Block(4,18,4,18);Block(12,18,12,18);
                Block(11,33,11,35);Block(21,33,21,35);
            }
            else if(zone==Campaign.Cistern)
            {
                // Dry causeways surround three sunken basins; north/south crossovers form loops.
                Carve(2,1,9,9);Carve(4,8,6,15);Carve(2,14,14,24);
                Block(7,18,9,20);Carve(11,23,13,29);Carve(8,28,23,39);
                Block(14,32,16,34);Block(20,35,22,37);
                Carve(17,13,22,24);Carve(13,14,19,16);Carve(19,23,21,29);
            }
            else if(zone==Campaign.Ossuary)
            {
                // Three connected burial rotundas with staggered side galleries.
                Ellipse(5,6,4.5f,5.8f);Carve(4,10,6,15);
                Ellipse(8,19,7.5f,6.0f);Carve(11,23,13,29);
                Ellipse(16,34,7.4f,5.8f);Carve(17,14,22,20);Carve(14,17,18,19);
                Carve(2,27,6,33);Carve(4,23,6,29);Carve(6,30,11,32);
                Block(7,18,8,19);Block(16,32,17,33);
            }
            else if(zone==Campaign.Kings)
            {
                Ellipse(5,6,4.3f,5.6f);Carve(4,10,6,14);Carve(2,14,15,24);
                Carve(12,24,14,29);Carve(8,29,23,39);
                Carve(17,13,22,21);Carve(14,16,18,18);Carve(19,21,21,30);
                Block(4,18,4,18);Block(12,18,12,18);Block(11,32,11,32);Block(20,32,20,32);Block(11,36,11,36);Block(20,36,20,36);
            }
            else
            {
                Carve(1,1,9,9);Carve(4,10,6,13);Carve(1,14,15,24);
                Carve(12,25,14,28);Carve(8,29,23,39);
                floor[4,18]=floor[12,18]=floor[11,32]=floor[20,32]=floor[11,36]=floor[20,36]=false;
                if(zone==Campaign.Crypt2){Carve(17,16,22,23);Carve(15,19,18,21);Block(6,16,8,19);Carve(1,20,4,24);}
                if(zone==Campaign.Crypt3){Carve(1,27,6,33);Carve(4,23,6,28);Carve(6,30,9,32);Carve(18,18,22,24);Carve(14,21,19,23);}
            }
            EnsureLegacyPins();BuildDistances(5,3);
        }

        bool BuildLongRoute(int zone)
        {
            if(zone==Campaign.Ravine)
            {
                // Four mountain switchbacks. The empty bands are cliffs, not shortcuts.
                Trail(1.6f,5,3,7,9,20,9,20,18,5,18,5,25,17,23,20,28,10,34,17,38);
                Ellipse(5,4,3.1f,3.4f);Ellipse(7,9,3.1f,2.7f);Ellipse(20,15,2.7f,4.1f);
                Ellipse(5,23,2.6f,3.8f);Ellipse(17,24,3.0f,2.1f);Ellipse(10,34,2.4f,2.8f);Ellipse(17,38,2.6f,1.7f);
                // A true dead end below a rock shelf; treasure never creates a through route.
                Trail(1.05f,12,9,12,4,16,3);Ellipse(17,3,2.0f,1.6f);
                Trail(1.1f,20,28,22,33);return true;
            }
            if(zone==Campaign.Cloister)
            {
                // Ruined courts alternate sides around the inaccessible chapel footprint.
                Carve(2,1,9,10);Carve(8,8,20,10);Carve(17,9,21,16);
                Carve(4,14,18,16);Carve(3,15,7,24);Carve(6,22,19,24);
                Carve(18,23,22,29);Carve(9,27,20,29);Carve(8,28,12,35);
                Carve(11,33,14,35);Carve(13,34,20,39);
                // A collapsed west cell and a small northern bell court.
                Carve(1,25,5,29);Carve(3,23,5,26);Carve(19,3,23,6);Carve(19,5,21,10);
                Block(5,5,5,5);Block(19,12,19,12);Block(5,19,5,19);return true;
            }
            if(zone==Campaign.Galleries)
            {
                // Offset burial galleries with five doors and a blind western ossuary.
                Carve(2,1,9,10);Carve(8,7,20,9);Carve(17,8,21,17);
                Carve(5,15,18,18);Carve(3,16,7,25);Carve(6,22,19,24);
                Carve(17,23,22,31);Carve(8,29,19,31);Carve(7,30,12,36);
                Carve(11,35,20,39);Carve(1,27,4,31);Carve(3,24,5,28);
                Block(19,13,19,13);Block(5,20,5,20);Block(19,27,19,27);return true;
            }
            if(zone==Campaign.Forge)
            {
                // Dog-leg service halls pass three furnaces; molten wells remain sealed.
                Carve(2,1,9,10);Carve(8,7,21,9);Carve(19,8,21,17);
                Carve(4,15,20,18);Carve(3,17,6,26);Carve(5,24,18,26);
                Carve(15,22,19,26);Carve(17,25,21,31);Carve(8,29,19,32);
                Carve(7,31,12,36);Carve(11,35,19,39);
                Carve(12,2,16,5);Carve(13,4,15,8);Carve(1,29,4,33);Carve(3,25,5,30);
                Block(12,16,13,17);Block(18,28,18,28);return true;
            }
            if(zone==Campaign.Threshold)
            {
                // A ritual procession must cross the western vault, then return east.
                Ellipse(5,5,3.8f,4.5f);Carve(4,6,8,11);Carve(7,8,20,10);
                Carve(18,9,21,24);Carve(4,21,19,24);Carve(3,23,6,35);
                Carve(5,32,11,35);Carve(9,28,11,34);Carve(10,28,21,30);
                Carve(19,29,22,38);Carve(15,36,22,39);
                Carve(1,13,5,17);Carve(3,16,5,22);
                // The solid 25-27 band stops a direct jump from second to final rite.
                Block(12,25,23,27);Block(19,15,19,15);return true;
            }
            return false;
        }

        void Ellipse(int cx,int cz,float rx,float rz)
        {
            for(int x=1;x<Width-1;x++)for(int z=1;z<Height-1;z++)
            {float dx=(x-cx)/rx,dz=(z-cz)/rz;if(dx*dx+dz*dz<=1)floor[x,z]=true;}
        }
        void Trail(float radius,params int[] points)
        {
            for(int i=0;i<points.Length-2;i+=2)
            {
                float ax=points[i],az=points[i+1],bx=points[i+2],bz=points[i+3],vx=bx-ax,vz=bz-az,len=vx*vx+vz*vz;
                for(int x=1;x<Width-1;x++)for(int z=1;z<Height-1;z++)
                {float t=len<.01f?0:Math.Max(0,Math.Min(1,((x-ax)*vx+(z-az)*vz)/len));float dx=x-ax-vx*t,dz=z-az-vz*t;if(dx*dx+dz*dz<=radius*radius)floor[x,z]=true;}
            }
        }
        void EnsureLegacyPins()
        {
            // Only short alcoves are added; this keeps existing encounter/NPC/save contracts.
            int[,] pins={{5,3},{17,38},{3,7},{7,8},{5,9},{3,16},{10,16},{6,21},{13,23},{12,30},{18,31},{21,34},{15,35},{17,36},{7,6},{3,22},{21,30},{20,35}};
            for(int i=0;i<pins.GetLength(0);i++)
            {int x=pins[i,0],z=pins[i,1];if(Walkable(x,z))continue;Cell near=NearestWalkable(x,z);Trail(1.05f,near.X,near.Z,x,z);}
        }
        public Cell NearestWalkable(int x,int z)
        {
            Cell best=new Cell(-1,-1);int score=int.MaxValue;
            for(int a=1;a<Width-1;a++)for(int b=1;b<Height-1;b++)
            {if(!Walkable(a,b))continue;int d=(a-x)*(a-x)+(b-z)*(b-z);if(d<score){score=d;best=new Cell(a,b);}}
            return best;
        }

        void Block(int x1,int z1,int x2,int z2)
        {
            for (int x=x1;x<=x2;x++) for (int z=z1;z<=z2;z++) floor[x,z]=false;
        }

        void Carve(int x1, int z1, int x2, int z2)
        {
            for (int x = x1; x <= x2; x++)
                for (int z = z1; z <= z2; z++) floor[x, z] = true;
        }

        public bool Walkable(int x, int z)
        {
            return x >= 0 && x < Width && z >= 0 && z < Height && floor[x, z];
        }
        public void OpenCell(int x,int z)
        {if(x>0 && x<Width-1 && z>0 && z<Height-1)floor[x,z]=true;}

        public void CloseCell(int x,int z)
        {if(x>0 && x<Width-1 && z>0 && z<Height-1)floor[x,z]=false;}

        public bool Border(int x, int z)
        {
            if (Walkable(x, z)) return false;
            for (int i = 0; i < 4; i++)
                if (Walkable(x + DX[i], z + DZ[i])) return true;
            return false;
        }

        public bool CanStand(float x, float z, float radius)
        {
            return Point(x - radius, z - radius) && Point(x + radius, z - radius)
                && Point(x - radius, z + radius) && Point(x + radius, z + radius);
        }

        bool Point(float x, float z)
        {
            return Walkable((int)Math.Floor(x + .5f), (int)Math.Floor(z + .5f));
        }

        public bool Visible(float x1, float z1, float x2, float z2)
        {
            return CanTravel(x1,z1,x2,z2,0);
        }

        public bool CanTravel(float x1, float z1, float x2, float z2, float radius)
        {
            int samples = Math.Max(1, (int)(Math.Sqrt((x2-x1)*(x2-x1)+(z2-z1)*(z2-z1)) * 8));
            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;
                if (!CanStand(x1 + (x2-x1)*t, z1 + (z2-z1)*t, radius)) return false;
            }
            return true;
        }

        // Follow the distance field using a collision-checked waypoint. Looking ahead
        // avoids changing to a cell centre behind the actor at a corridor corner.
        // The live collision grid stays separate from the target's distance field.
        public bool TryWaypoint(DungeonGrid route,float x,float z,float goalX,float goalZ,float radius,out float waypointX,out float waypointZ)
        {
            waypointX=x;waypointZ=z;
            if(CanTravel(x,z,goalX,goalZ,radius)){waypointX=goalX;waypointZ=goalZ;return true;}
            int cx=(int)Math.Floor(x+.5f),cz=(int)Math.Floor(z+.5f);
            if(route==null || route.Distance(cx,cz)<0)return false;
            int nx=cx,nz=cz;bool found=false;
            for(int i=0;i<12;i++)
            {
                Cell next;if(!route.Next(nx,nz,out next))break;nx=next.X;nz=next.Z;
                if(CanTravel(x,z,nx,nz,radius)){waypointX=nx;waypointZ=nz;found=true;}
            }
            if(found)return true;
            // An off-centre actor may first need to clear the inside of a corner.
            // Never steer to that centre if a live container now blocks it.
            // Movement uses a slightly smaller body than the route clearance. If
            // manual movement leaves the body close to a wall, CanTravel rejects
            // every ray at sample zero. Moving toward its own walkable cell centre
            // monotonically increases wall clearance and lets navigation recover.
            if(Walkable(cx,cz) && Math.Abs(x-cx)+Math.Abs(z-cz)>.001f)
            {waypointX=cx;waypointZ=cz;return true;}
            return false;
        }

        public void BuildDistances(int targetX, int targetZ)
        {
            for (int x = 0; x < Width; x++)
                for (int z = 0; z < Height; z++) distance[x, z] = -1;
            if (!Walkable(targetX, targetZ)) return;
            Queue<Cell> queue = new Queue<Cell>();
            queue.Enqueue(new Cell(targetX, targetZ));
            distance[targetX, targetZ] = 0;
            while (queue.Count > 0)
            {
                Cell c = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int x = c.X + DX[i], z = c.Z + DZ[i];
                    if (!Walkable(x, z) || distance[x, z] >= 0) continue;
                    distance[x, z] = distance[c.X, c.Z] + 1;
                    queue.Enqueue(new Cell(x, z));
                }
            }
        }

        public int Distance(int x, int z)
        {
            return Walkable(x, z) ? distance[x, z] : -1;
        }

        public bool Next(int x, int z, out Cell next)
        {
            next = new Cell(x, z);
            int best = Distance(x, z);
            if (best <= 0) return false;
            for (int i = 0; i < 4; i++)
            {
                int nx = x + DX[i], nz = z + DZ[i], d = Distance(nx, nz);
                if (d >= 0 && d < best) { best = d; next = new Cell(nx, nz); }
            }
            return next.X != x || next.Z != z;
        }
    }
}
