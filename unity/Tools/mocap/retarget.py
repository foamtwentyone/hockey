import numpy as np
from bvh import BVH
from kk import KK
KKP='../kaykit/repo/addons/kaykit_character_pack_adventures/Characters/gltf/'
def yaw_m(a):
    c,s=np.cos(a),np.sin(a); return np.array([[c,0,s],[0,1,0],[-s,0,c]])
LIMBS=[('upperarm.l','lowerarm.l','LeftArm','LeftForeArm'),('lowerarm.l','wrist.l','LeftForeArm','LeftHand'),
       ('upperarm.r','lowerarm.r','RightArm','RightForeArm'),('lowerarm.r','wrist.r','RightForeArm','RightHand'),
       ('upperleg.l','lowerleg.l','LeftUpLeg','LeftLeg'),('lowerleg.l','foot.l','LeftLeg','LeftFoot'),('foot.l','toes.l','LeftFoot','LeftToeBase'),
       ('upperleg.r','lowerleg.r','RightUpLeg','RightLeg'),('lowerleg.r','foot.r','RightLeg','RightFoot'),('foot.r','toes.r','RightFoot','RightToeBase')]
MIRROR=False
def targets(b,f,yaw):
    """Мировые направления из кадра BVH (повёрнутые на yaw), в системе персонажа: x — влево персонажа(.l), y — вверх, z — вперёд."""
    P,_=b.fk(f); Y=yaw_m(yaw)
    if MIRROR:
        sw=lambda n: n.replace('Left','#').replace('Right','Left').replace('#','Right')
        g=lambda n:(lambda v: np.array([-v[0],v[1],v[2]]))(Y@P[b.idx(sw(n))])
    else: g=lambda n:Y@P[b.idx(n)]
    t={}
    t['hips_up']=g('Spine')-g('Hips'); t['hips_side']=g('LeftUpLeg')-g('RightUpLeg')
    t['spine']=g('Spine1')-g('LowerBack'); t['chest']=g('Neck1')-g('Spine1'); t['chest_side']=g('LeftArm')-g('RightArm')
    for kb,kc,b0,b1 in LIMBS: t[kb]=g(b1)-g(b0)
    t['hipsY']=g('Hips')[1]; t['hipsXZ']=g('Hips')[[0,2]]
    t['legLen']=np.linalg.norm(g('LeftLeg')-g('LeftUpLeg'))+np.linalg.norm(g('LeftFoot')-g('LeftLeg'))
    t['lh']=g('LeftHand'); t['rh']=g('RightHand')
    return t
def apply(k,t,t0):
    k.R=[r.copy() for r in k.R0]; k.fk()
    # высота таза: относительно позы покоя, в масштабе длины ног
    kleg=np.linalg.norm(k.WP[k.idx('lowerleg.l')]-k.WP[k.idx('upperleg.l')])+np.linalg.norm(k.WP[k.idx('foot.l')]-k.WP[k.idx('lowerleg.l')])
    sc=kleg/t0['legLen']
    hi=k.idx('hips'); k.T[hi]=k.T[hi]*0+np.array(k.g.j['nodes'][hi].get('translation',[0,0,0]))
    k.T[hi][1]+= (t['hipsY']-t0['hipsY'])*sc
    k.aim('hips','spine',t['hips_up'])
    k.twist('hips','hips','spine',lambda:k.WP[k.idx('upperleg.l')]-k.WP[k.idx('upperleg.r')],t['hips_side'])
    k.aim('spine','chest',t['spine']); k.aim('chest','head',t['chest'])
    k.twist('chest','chest','head',lambda:k.WP[k.idx('upperarm.l')]-k.WP[k.idx('upperarm.r')],t['chest_side'])
    for kb,kc,_,_ in LIMBS: k.aim(kb,kc,t[kb])

def facing_yaw(b,f):
    P,_=b.fk(f); side=P[b.idx('LeftUpLeg')]-P[b.idx('RightUpLeg')]
    fwd=np.cross(side,[0,1,0])   # для лица по +Z при левом бедре по +X: cross(+X, +Y) = +Z
    return -np.arctan2(fwd[0],fwd[2])
def stick_lines(k, top='hand.r', low='hand.l', length=1.35):
    T=k.WP[k.idx(top)]; B=k.WP[k.idx(low)]; d=B-T; d/=np.linalg.norm(d)
    # древко от верхней руки через нижнюю до льда (y=0) или на длину клюшки
    L=length
    if d[1]<-0.05: L=min(L,(T[1]-0.02)/-d[1])
    end=T+d*L
    toe=end+np.cross([0,1,0],d)*0  # крюк рисуем отдельно
    return [(T-d*0.08,end,(30,30,40),5),(end,end+np.array([-0.25,0,0.08]),(30,30,40),5)]
