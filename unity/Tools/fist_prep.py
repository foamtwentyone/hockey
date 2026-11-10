# Крага-кулак: убрать зелёную палку, найти её ось и центр, поставить центр хвата в начало координат.
import sys, numpy as np, trimesh
m=trimesh.load(sys.argv[1],force='mesh')
img=np.asarray(m.visual.material.baseColorTexture.convert('RGB')).astype(float); h,w=img.shape[:2]
uv=m.visual.uv; F=m.faces
samp=[]
for wts in ([1/3,1/3,1/3],[.6,.2,.2],[.2,.6,.2],[.2,.2,.6]):
    p=(uv[F]*np.array(wts)[None,:,None]).sum(1)
    samp.append(img[np.clip(((1-p[:,1]%1)*(h-1)).astype(int),0,h-1),np.clip(((p[:,0]%1)*(w-1)).astype(int),0,w-1)])
c=np.median(np.stack(samp),0)
green=(c[:,1]>140)&(c[:,0]<130)&(c[:,2]<130)&(c[:,1]-np.maximum(c[:,0],c[:,2])>60)
cent=m.vertices[F].mean(1)
G=cent[green]; ctr=G.mean(0)
u,s,vt=np.linalg.svd(G-ctr); axis=vt[0]
print('green faces',green.sum(),'of',len(F),'center',ctr.round(3),'axis',axis.round(3))
# центр хвата: середина палки внутри кулака — берём точки палки, закрытые кулаком (ближе к центру по оси)
t=(G-ctr)@axis
print('rod extent', t.min().round(3), t.max().round(3))
m.update_faces(~green); m.remove_unreferenced_vertices()
# ось палки → X (знак сохраняем), центр → 0
ax=axis if axis[0]>=0 else -axis
R=trimesh.geometry.align_vectors(ax,[1,0,0])
T=np.eye(4); T[:3,3]=-ctr
m.apply_transform(T); m.apply_transform(R)
print('bounds after', m.bounds.round(3))
m.export(sys.argv[2])
