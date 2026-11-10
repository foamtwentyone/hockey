# Экспорт мокапа CMU в направления костей (система персонажа: x — к левому боку, y — вверх, z — вперёд)
import sys, numpy as np, retarget
from retarget import *
OUT='/home/user/hockey/unity/Assets/Hockey/Resources/Anim/'
import os; os.makedirs(OUT,exist_ok=True)
def nz(v): v=np.array(v,float); return v/np.linalg.norm(v)
def export(name, path, f0, f1, mirror, extra_yaw, fps=30, smooth_yaw_frame=None):
    retarget.MIRROR=mirror
    b=BVH(path); step=max(1,int(round(1/(fps*b.dt))))
    yaw=facing_yaw(b, smooth_yaw_frame or f0)+extra_yaw
    rows=[]
    for f in range(f0,f1+1,step):
        t=targets(b,f,yaw)
        P,_=b.fk(f)
        arm=t['upperarm.l']; armLen=np.linalg.norm(t['upperarm.l'])+np.linalg.norm(t['lowerarm.l'])
        top,low=t['rh'],t['lh']   # правая рука сверху, левая снизу (после отражения у гольфа тоже)
        sd=low-top; sep=np.linalg.norm(sd)/armLen
        vals=[]
        for key in ['hips_up','hips_side','spine','chest','chest_side','upperarm.l','lowerarm.l','upperarm.r','lowerarm.r']: vals+=list(nz(t[key]))
        vals+=list(nz(sd)); vals.append(sep)
        rows.append(' '.join(f'{v:.4f}' for v in vals))
    open(OUT+name+'.txt','w').write(f'{name} {fps} {len(rows)}\n'+'\n'.join(rows)+'\n')
    print(name,len(rows))
CMU='../cmu/data/'
export('stance', CMU+'013/13_23.bvh', 100, 140, False, 0.0)
export('windup', CMU+'064/64_01.bvh', 150, 262, True, np.pi/2, smooth_yaw_frame=150)
export('shot',   CMU+'064/64_01.bvh', 262, 360, True, np.pi/2, smooth_yaw_frame=150)
