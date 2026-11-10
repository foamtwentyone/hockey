# серые области: крупные «панели» снаружи → белые (как на образце), тонкие швы и внутренность раструба — серые
import sys, numpy as np, trimesh
from PIL import Image, ImageDraw, ImageFilter
base=sys.argv[1]; thr=float(sys.argv[2])
m=trimesh.load(base+'.obj',force='mesh',process=False)
img=Image.open(base+'.png').convert('RGB'); w,h=img.size; a=np.asarray(img).astype(float)
uv=m.visual.uv; F=m.faces; V=m.vertices
p=uv[F].mean(1); c=a[np.clip(((1-p[:,1]%1)*(h-1)).astype(int),0,h-1),np.clip(((p[:,0]%1)*(w-1)).astype(int),0,w-1)]
pal=np.array([[0xf2,0xf2,0xf2],[0x9a,0x9a,0xa0],[0xd6,0xb0,0x80]],float)
cls=((c[:,None]-pal[None])**2).sum(-1).argmin(1)
vid=np.unique(np.round(V,5),axis=0,return_inverse=True)[1].ravel(); Fv=vid[F]
edges={}
for fi,(x,y,z) in enumerate(Fv):
    for e in ((x,y),(y,z),(z,x)): edges.setdefault((min(e),max(e)),[]).append(fi)
nb=[[] for _ in F]
for fs in edges.values():
    for i in fs:
        for j in fs:
            if i!=j: nb[i].append(j)
area=m.area_faces; cy=V[F].mean(1)[:,1]
# «толщина» серой области: сколько соседей на 2 шага тоже серые (тонкий шов — мало)
grey=cls==1
deep=np.zeros(len(F),bool)
for i in np.where(grey)[0]:
    ring=set(nb[i])
    for j in list(ring): ring.update(nb[j])
    ring.discard(i)
    if ring and np.mean([grey[j] for j in ring])>thr: deep[i]=True
cuffY=V[:,1].min()+0.2*(V[:,1].max()-V[:,1].min())
fn=m.face_normals; ctr=V.mean(0)
outward=((V[F].mean(1)-ctr)*fn).sum(1)>0
sel=deep & (cy>cuffY) & outward
cls[sel]=0
print('grey->white',sel.sum(),'grey left',(cls==1).sum())
out=Image.new('RGB',(w,h),tuple(int(x) for x in pal[0])); mask=Image.new('L',(w,h),0)
d=ImageDraw.Draw(out); dm=ImageDraw.Draw(mask)
for t in range(len(F)):
    pts=[((uv[j,0]%1)*w,(1-uv[j,1]%1)*h) for j in F[t]]; col=tuple(int(x) for x in pal[cls[t]])
    d.polygon(pts,fill=col,outline=col); dm.polygon(pts,fill=255,outline=255)
arr=np.asarray(out).copy(); mk=np.asarray(mask)>0
for _ in range(6):
    gm=np.asarray(Image.fromarray((mk*255).astype(np.uint8)).filter(ImageFilter.MaxFilter(3)))>0
    new=gm&~mk; med=np.asarray(Image.fromarray(arr).filter(ImageFilter.ModeFilter(3))); arr[new]=med[new]; mk=gm
Image.fromarray(arr).filter(ImageFilter.GaussianBlur(0.8)).save(base+'.png')
