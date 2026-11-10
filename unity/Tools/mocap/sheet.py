import sys, numpy as np
from PIL import Image, ImageDraw
from bvh import BVH
path, step, out = sys.argv[1], int(sys.argv[2]), sys.argv[3]
start=int(sys.argv[4]) if len(sys.argv)>4 else 1
end=int(sys.argv[5]) if len(sys.argv)>5 else None
b=BVH(path); end=end or len(b.data)
frames=list(range(start,end,step))[:24]
cols=6; W=180; H=220
img=Image.new('RGB',(cols*W, ((len(frames)+cols-1)//cols)*H*2),(30,30,40)); d=ImageDraw.Draw(img)
P0,_=b.fk(start)
for k,f in enumerate(frames):
    P,R=b.fk(f)
    for v,(ax,ay) in enumerate([(0,1),(2,1)]):   # фронт (x,y) и бок (z,y)
        ox=(k%cols)*W+W/2; oy=(k//cols)*H*2+v*H+H-15
        c=P[0]
        sc=4.2
        def pt(p): return (ox+(p[ax]-c[ax])*sc*(1 if v==0 else 1), oy-(p[ay])*sc+ (0))
        for j,pa in enumerate(b.parent):
            if pa<0: continue
            col=(255,90,90) if 'Left' in b.names[j] or b.names[j].startswith('L') else (90,160,255) if 'Right' in b.names[j] or b.names[j].startswith('R') else (230,230,230)
            d.line([pt(P[pa]),pt(P[j])],fill=col,width=2)
        d.text((ox-80,oy-H+18),f'f{f} {"front" if v==0 else "side"}',fill=(200,200,200))
img.save(out)
