import math, os
verts=[]; out=[]
def v(x,y,z):
    verts.append((x,y,z)); return len(verts)
def g(name): out.append(('g',name))
def m(name): out.append(('m',name))
def f(ids): out.append(tuple(ids))

def ellipsoid(name,c,r,mat,nu=10,nv=6):
    g(name); m(mat)
    rings=[]; cx,cy,cz=c; rx,ry,rz=r
    for j in range(nv+1):
        phi=-math.pi/2+math.pi*j/nv
        ring=[]
        for i in range(nu):
            theta=2*math.pi*i/nu
            ring.append(v(cx+rx*math.cos(phi)*math.cos(theta),cy+ry*math.sin(phi),cz+rz*math.cos(phi)*math.sin(theta)))
        rings.append(ring)
    for j in range(nv):
        for i in range(nu):
            a,b,c1,d=rings[j][i],rings[j][(i+1)%nu],rings[j+1][(i+1)%nu],rings[j+1][i]
            f([a,b,c1,d])

def cyl(name,p0,p1,r,mat,n=8):
    g(name); m(mat)
    x0,y0,z0=p0; x1,y1,z1=p1
    # just make horizontal circle in xz mostly; for our limbs/tail good enough
    ring0=[]; ring1=[]
    for i in range(n):
        t=2*math.pi*i/n
        dx=r*math.cos(t); dz=r*math.sin(t)
        ring0.append(v(x0+dx,y0,z0+dz)); ring1.append(v(x1+dx,y1,z1+dz))
    for i in range(n): f([ring0[i],ring0[(i+1)%n],ring1[(i+1)%n],ring1[i]])
    f(list(reversed(ring0))); f(ring1)

def quad(name, pts, mat):
    g(name); m(mat); ids=[v(*p) for p in pts]; f(ids); f(list(reversed(ids)))

ellipsoid('Body_spine_rig_section',(0,1.0,0),(0.48,0.36,0.92),'fur_tan',14,7)
ellipsoid('Chest_shoulder_loop',(0,1.10,0.56),(0.44,0.38,0.36),'fur_tan')
cyl('Neck_bone_area',(0,1.34,0.68),(0,1.58,0.92),0.24,'fur_tan')
ellipsoid('Head_skull',(0,1.72,1.18),(0.36,0.32,0.36),'fur_tan')
ellipsoid('Muzzle_beard_area',(0,1.61,1.50),(0.24,0.17,0.28),'fur_light')
ellipsoid('Nose',(0,1.62,1.76),(0.10,0.06,0.06),'nose_black',8,4)
ellipsoid('Eye_L',(-0.14,1.82,1.45),(0.04,0.045,0.03),'eye_black',6,4)
ellipsoid('Eye_R',(0.14,1.82,1.45),(0.04,0.045,0.03),'eye_black',6,4)
quad('Ear_L_rig_flap',[(-0.22,1.98,1.08),(-0.48,1.86,1.20),(-0.34,1.48,1.14),(-0.16,1.72,1.20)],'fur_dark')
quad('Ear_R_rig_flap',[(0.22,1.98,1.08),(0.48,1.86,1.20),(0.34,1.48,1.14),(0.16,1.72,1.20)],'fur_dark')
for nm,x,z in [('Front_L',-.28,.50),('Front_R',.28,.50),('Back_L',-.30,-.58),('Back_R',.30,-.58)]:
    cyl(nm+'_UpperLeg',(x,.92,z),(x,.52,z),.12,'fur_tan')
    cyl(nm+'_LowerLeg',(x,.52,z),(x,.18,z+.05),.095,'fur_tan')
    ellipsoid(nm+'_Paw',(x,.10,z+.14),(.16,.07,.21),'fur_light',8,4)
cyl('Tail_base',(0,1.12,-.86),(0,1.42,-1.12),.09,'fur_tan')
cyl('Tail_mid',(0,1.42,-1.12),(0,1.72,-1.08),.07,'fur_tan')
# rig guide marker cubes as tiny tetra-ish ellipsoids
for nm,c in [('RigGuide_pelvis',(0,1.05,-.52)),('RigGuide_chest',(0,1.22,.52)),('RigGuide_head',(0,1.72,1.18))]: ellipsoid(nm,c,(.04,.04,.04),'guide_blue',6,3)
with open('/mnt/data/terrier_rig_friendly.obj','w') as fh:
    fh.write('# Rig-friendly low-poly terrier dog OBJ\n# Y-up, Z-forward. Use named groups as rigging/weight-painting sections.\nmtllib terrier_rig_friendly.mtl\n')
    for vv in verts: fh.write('v %.5f %.5f %.5f\n'%vv)
    for item in out:
        if item[0]=='g': fh.write('g '+item[1]+'\n')
        elif item[0]=='m': fh.write('usemtl '+item[1]+'\n')
        else: fh.write('f '+' '.join(map(str,item))+'\n')
with open('/mnt/data/terrier_rig_friendly.mtl','w') as fh:
    fh.write('newmtl fur_tan\nKd 0.67 0.49 0.31\n\nnewmtl fur_light\nKd 0.92 0.82 0.64\n\nnewmtl fur_dark\nKd 0.28 0.19 0.12\n\nnewmtl nose_black\nKd 0.02 0.018 0.015\n\nnewmtl eye_black\nKd 0.01 0.01 0.012\n\nnewmtl guide_blue\nKd 0.10 0.35 0.95\n')
with open('/mnt/data/terrier_rigging_notes.txt','w') as fh:
    fh.write('Rigging notes: OBJ has named groups for body, chest, neck, head, ears, tail, upper/lower legs, and paws. OBJ does not contain bones. In Blender, add an armature with pelvis->spine->chest->neck->head, four leg chains, tail chain, and optional ear flap bones. RigGuide_* markers indicate rough joint placement and may be deleted before export to Unity.\n')
print('ok', len(verts), len([x for x in out if x and x[0] not in ('g','m')]))
