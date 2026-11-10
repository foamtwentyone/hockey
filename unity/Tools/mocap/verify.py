import sys, numpy as np
from PIL import Image, ImageDraw
from kk import KK
from retarget import KKP
name=sys.argv[1]; idxs=list(map(int,sys.argv[2].split(','))); out=sys.argv[3]
L=open('/home/user/hockey/unity/Assets/Hockey/Resources/Anim/'+name+'.txt').read().split('\n')
rows=[np.array(list(map(float,l.split()))) for l in L[1:] if l.strip()]
k=KK(KKP+'Rogue.glb',KKP+'rogue_texture.png')
views=[([1,0,0],[0,1,0],[0,0,1]),([0,0,-1],[0,1,0],[1,0,0]),([-0.7,0,0.7],[0,1,0],[-0.7,0.25,-0.7])]
S=230; img=Image.new('RGB',(S*len(idxs),S*3))
for i,ri in enumerate(idxs):
    r=rows[ri]; v=lambda j: r[j*3:j*3+3]
    k.R=[x.copy() for x in k.R0]; k.fk()
    k.aim('hips','spine',v(0))
    k.twist('hips','hips','spine',lambda:k.WP[k.idx('upperleg.l')]-k.WP[k.idx('upperleg.r')],v(1))
    k.aim('spine','chest',v(2)); k.aim('chest','head',v(3))
    k.twist('chest','chest','head',lambda:k.WP[k.idx('upperarm.l')]-k.WP[k.idx('upperarm.r')],v(4))
    k.aim('upperarm.l','lowerarm.l',v(5)); k.aim('lowerarm.l','wrist.l',v(6)); k.aim('upperarm.r','lowerarm.r',v(7)); k.aim('lowerarm.r','wrist.r',v(8))
    top=k.WP[k.idx('hand.r')]; d=v(9); end=top+d*1.3
    if d[1]<-0.05: end=top+d*min(1.3,(top[1]-0.02)/-d[1])
    lines=[(top,end,(20,20,30),5),(np.array([0,0.02,0.6]),np.array([0,0.02,1.2]),(255,220,60),3)]
    for vi,vw in enumerate(views):
        im=k.render(vw,S,center=np.array([0,0.6,0.25]),scale=S*0.3,lines=lines)
        ImageDraw.Draw(im).text((4,4),f'{name} {ri}',fill=(230,230,230)); img.paste(im,(i*S,vi*S))
img.save(out)
