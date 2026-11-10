# Экспорт вручную собранных хоккейных поз (pose.py) в клипы Resources/Anim/*.txt (формат MocapPose.cs)
import numpy as np, copy
from pose import *
OUT='/home/user/hockey/unity/Assets/Hockey/Resources/Anim/'
def lerpP(A,B,t):
    R={}
    for key in set(A)|set(B):
        a=np.array(A.get(key,B.get(key)),float); b=np.array(B.get(key,A.get(key)),float); R[key]=a+(b-a)*t
    R['sep']=float(R['sep']); return R
def ease(t): return t*t*(3-2*t)
def row(k,P):
    top,low,blade,d=pose(k,P)
    g=lambda n:k.WP[k.idx(n)]
    v=[g('spine')-g('hips'), g('upperleg.l')-g('upperleg.r'), g('chest')-g('spine'), g('head')-g('chest'), g('upperarm.l')-g('upperarm.r'),
       g('lowerarm.l')-g('upperarm.l'), g('wrist.l')-g('lowerarm.l'), g('lowerarm.r')-g('upperarm.r'), g('wrist.r')-g('lowerarm.r'), d]
    arm=np.linalg.norm(g('lowerarm.l')-g('upperarm.l'))+np.linalg.norm(g('wrist.l')-g('lowerarm.l'))
    vals=[]
    for x in v: vals+=list(nz(x))
    vals.append(P['sep']/arm)
    return ' '.join(f'{x:.4f}' for x in vals)
def clip(name,keys,n,fps=30):
    k=new_kk(); rows=[]
    for i in range(n):
        t=i/(n-1)*(len(keys)-1); a=min(int(t),len(keys)-2)
        rows.append(row(k,lerpP(keys[a],keys[a+1],ease(t-a))))
    open(OUT+name+'.txt','w').write(f'{name} {fps} {n}\n'+'\n'.join(rows)+'\n'); print(name,n)
SWAY=copy.deepcopy(STANCE); SWAY['blade']=[0.3,0.03,1.05]; SWAY['top']=[-0.2,0.53,0.22]; SWAY['chestTwist']=[1,0,0.08]
clip('stance',[STANCE,SWAY],12)
clip('windup',[STANCE,WINDUP],20)
clip('shot',[WINDUP,CONTACT,FOLLOW,STANCE],30)
