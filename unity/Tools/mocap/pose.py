import sys, numpy as np
from PIL import Image, ImageDraw
from kk import KK
from retarget import KKP
ARM=1.3; HEAD=0.86
def nz(v): v=np.array(v,float); return v/np.linalg.norm(v)
def new_kk():
    k=KK(KKP+'Rogue.glb',KKP+'rogue_texture.png')
    for s in ('.l','.r'):
        for b in ('lowerarm','wrist','hand'): k.T[k.idx(b+s)]*=ARM
    k.S[k.idx('head')]=np.ones(3)*HEAD
    k.T0=k.T.copy(); k.fk(); return k
def ik(k,b0,b1,b2,target,pole):
    i0,i1,i2=k.idx(b0),k.idx(b1),k.idx(b2); k.fk()
    a=k.WP[i0]; l1=np.linalg.norm(k.WP[i1]-a); l2=np.linalg.norm(k.WP[i2]-k.WP[i1])
    at=np.array(target,float)-a; d=min(max(np.linalg.norm(at),abs(l1-l2)+1e-4),(l1+l2)*0.999); dr=nz(at)
    p=np.array(pole,float); p=p-dr*np.dot(p,dr); p=nz(p)
    x=(l1*l1-l2*l2+d*d)/(2*d); h=np.sqrt(max(0,l1*l1-x*x)); el=a+dr*x+p*h
    k.aim(b0,b1,el-a); k.aim(b1,b2,a+dr*d-el)
def pose(k,P):
    k.R=[r.copy() for r in k.R0]; k.T=k.T0.copy()
    hi=k.idx('hips'); k.T[hi]=k.T0[hi]+np.array(P.get('hips',[0,-0.07,-0.04])); k.fk()
    k.aim('hips','spine',P.get('pelvis',[0,1,0.18]))
    if 'hipTwist' in P: k.twist('hips','hips','spine',lambda:k.WP[k.idx('upperleg.l')]-k.WP[k.idx('upperleg.r')],P['hipTwist'])
    k.aim('spine','chest',P.get('spine',[0,1,0.5]))
    k.aim('chest','head',P.get('neck',[0,1,0.12]))
    if 'chestTwist' in P: k.twist('chest','chest','head',lambda:k.WP[k.idx('upperarm.l')]-k.WP[k.idx('upperarm.r')],P['chestTwist'])
    for s,sx in (('.l',1),('.r',-1)):
        f=P.get('foot'+s,[sx*0.2,0.145,0.0]); ik(k,'upperleg'+s,'lowerleg'+s,'foot'+s,f,[sx*0.25,0,1])
        k.aim('foot'+s,'toes'+s,P.get('toe'+s,[sx*0.3,0,1]))
    top=np.array(P['top'],float); blade=np.array(P['blade'],float); d=nz(blade-top); low=top+d*P.get('sep',0.42)
    # кисти: запястье чуть ближе к плечу, чем точка хвата
    for s,pt,sx in (('.r',top,-1),('.l',low,1)):
        k.fk(); sh=k.WP[k.idx('upperarm'+s)]; w=pt-nz(pt-sh)*0.05
        ik(k,'upperarm'+s,'lowerarm'+s,'hand'+s,w,P.get('elbow'+s,[sx*1,-0.5,-0.3]))
    k.fk()
    if P.get('_dbg'):
        for sfx,pt in (('.r',top),('.l',low)):
            g=lambda n:k.WP[k.idx(n)]
            L=np.linalg.norm(g('lowerarm'+sfx)-g('upperarm'+sfx))+np.linalg.norm(g('hand'+sfx)-g('lowerarm'+sfx))
            print(P['_dbg'],sfx,'reach',round(np.linalg.norm(pt-g('upperarm'+sfx))/L,2))
    return top,low,blade,d
VIEWS=[([1,0,0],[0,1,0],[0,0,1]),([0,0,-1],[0,1,0],[1,0,0]),([0.75,0,0.66],[0,1,0],[-0.66,0.3,0.75])]
def draw(k,info,S=440,label=''):
    top,low,blade,d=info
    butt=top-d*0.1; toe=blade+nz(np.cross([0,1,0],d))*0.0+np.array([-0.22,0,0.06])
    lines=[(butt,blade,(25,25,35),6),(blade,toe,(25,25,35),7)]
    for p in (top,low):
        for dx in ([0.05,0,0],[0,0.05,0],[0,0,0.05]): lines.append((p-np.array(dx),p+np.array(dx),(255,80,60),9))
    out=Image.new('RGB',(S*3,S))
    for i,v in enumerate(VIEWS):
        im=k.render(v,S,center=np.array([0.1,0.68,0.25]),scale=S*0.46,lines=lines)
        ImageDraw.Draw(im).text((5,5),label,fill=(255,255,255)); out.paste(im,(i*S,0))
    return out
STANCE=dict(top=[-0.16,0.6,0.26],blade=[0.45,0.03,1.0],sep=0.3,spine=[0,1,0.42],pelvis=[0,1,0.14],hips=[0,-0.08,-0.05])
STANCE['foot.l']=[0.25,0.145,0.05]; STANCE['foot.r']=[-0.25,0.145,-0.08]
FEET={'foot.l':[0.25,0.145,0.05],'foot.r':[-0.25,0.145,-0.08]}
STANCE.update(FEET)
WINDUP=dict(top=[0.0,0.72,0.3],blade=[0.8,1.0,-0.5],sep=0.34,spine=[0.08,1,0.36],pelvis=[0,1,0.12],hips=[0.02,-0.09,-0.05],chestTwist=[0.75,0,-0.65],neck=[-0.05,1,0.2],**{'elbow.l':[1,0.2,-0.6],'elbow.r':[-1,-0.6,0.2]},**FEET)
CONTACT=dict(top=[-0.14,0.62,0.3],blade=[0.38,0.03,0.75],sep=0.3,spine=[0.05,1,0.5],pelvis=[0,1,0.18],hips=[0.03,-0.11,0.0],chestTwist=[1,0,-0.1],**FEET)
FOLLOW=dict(top=[-0.14,0.7,0.2],blade=[0.05,0.5,1.3],sep=0.34,spine=[-0.05,1,0.5],pelvis=[0,1,0.2],hips=[0.0,-0.1,0.06],chestTwist=[0.8,0,0.6],**{'elbow.l':[1,-0.2,0.3],'elbow.r':[-1,-0.3,-0.5]},**FEET)
POSES=dict(stance=STANCE,windup=WINDUP,contact=CONTACT,follow=FOLLOW)
if __name__=='__main__':
    k=new_kk(); ims=[draw(k,pose(k,dict(P,_dbg=n)),S=330,label=n) for n,P in POSES.items()]
    out=Image.new('RGB',(ims[0].width,ims[0].height*len(ims)))
    for i,im in enumerate(ims): out.paste(im,(0,i*im.height))
    out.save(sys.argv[1] if len(sys.argv)>1 else 'pose.png')
