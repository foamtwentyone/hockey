# внутренность раструба — тёмная (снаружи не видно «пустую» серую трубу)
import sys, numpy as np, trimesh
from PIL import Image, ImageDraw
base=sys.argv[1]
m=trimesh.load(base+'.obj',force='mesh',process=False)
img=Image.open(base+'.png').convert('RGB'); w,h=img.size
V=m.vertices; F=m.faces; uv=m.visual.uv
cen=V[F].mean(1); fn=m.face_normals
ymin,ymax=V[:,1].min(),V[:,1].max(); cuffTop=ymin+0.32*(ymax-ymin)
# ось раструба: центр по x,z у низа модели
low=V[V[:,1]<ymin+0.1*(ymax-ymin)]; cx,cz=low[:,0].mean(),low[:,2].mean()
radial=np.c_[cen[:,0]-cx,np.zeros(len(cen)),cen[:,2]-cz]; radial/=np.linalg.norm(radial,axis=1)[:,None]+1e-9
inward=((fn*radial).sum(1)<-0.2)|(fn[:,1]>0.6)
rad=np.hypot(cen[:,0]-cx,cen[:,2]-cz); rOut=np.hypot(low[:,0]-cx,low[:,2]-cz).max()
ceiling=(fn[:,1]<-0.5)&(rad<rOut*0.78)&(cen[:,1]<ymin+0.55*(ymax-ymin))&(cen[:,1]>ymin+0.04*(ymax-ymin))
sel=((cen[:,1]<cuffTop)&inward)|ceiling
d=ImageDraw.Draw(img)
for t in np.where(sel)[0]:
    pts=[((uv[j,0]%1)*w,(1-uv[j,1]%1)*h) for j in F[t]]; d.polygon(pts,fill=(42,42,52),outline=(42,42,52))
img.save(base+'.png'); print('dark faces',sel.sum())
