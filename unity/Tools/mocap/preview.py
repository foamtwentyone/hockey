import sys, numpy as np
from PIL import Image, ImageDraw
import retarget
from retarget import *
retarget.MIRROR = len(sys.argv)>5 and sys.argv[5]=='m'
path=sys.argv[1]; frames=list(map(int,sys.argv[2].split(','))); yaw=sys.argv[3]; out=sys.argv[4]
b=BVH(path); k=KK(KKP+'Rogue.glb',KKP+'rogue_texture.png')
YW=lambda f: facing_yaw(b,f) if yaw=='auto' else float(yaw)
t0=targets(b,1,YW(1))
views=[([1,0,0],[0,1,0],[0,0,1]),([0,0,-1],[0,1,0],[1,0,0]),([0.7,0,0.7],[0,1,0],[0.7,0,-0.7])]
S=230; img=Image.new('RGB',(S*len(frames),S*3))
for i,f in enumerate(frames):
    apply(k,targets(b,f,YW(f)),t0)
    ln=stick_lines(k)
    for v,vw in enumerate(views):
        im=k.render(vw,S,center=np.array([0,0.6,0.2]),scale=S*0.32,lines=ln)
        ImageDraw.Draw(im).text((4,4),f'f{f}',fill=(220,220,220)); img.paste(im,(i*S,v*S))
img.save(out)
