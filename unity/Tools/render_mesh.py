import sys, numpy as np, trimesh
from PIL import Image, ImageDraw
TEX=None
def face_colors(m):
    src=Image.open(TEX) if TEX else m.visual.material.baseColorTexture
    img=np.asarray(src.convert('RGB')); h,w=img.shape[:2]
    uv=m.visual.uv[m.faces].mean(1)
    return img[np.clip(((1-uv[:,1]%1)*(h-1)).astype(int),0,h-1), np.clip(((uv[:,0]%1)*(w-1)).astype(int),0,w-1)]
def render(V,F,C,view,S=300):
    r,u,f=[np.array(x,float) for x in view]
    c=V.mean(0); sc=S*0.42/np.abs(V-c).max()
    img=Image.new('RGB',(S,S),(40,42,60)); d=ImageDraw.Draw(img)
    P=V[F]; n=np.cross(P[:,1]-P[:,0],P[:,2]-P[:,0]); nl=np.linalg.norm(n,axis=1)+1e-12; n/=nl[:,None]
    vis=(n@f)>0; dep=((P.mean(1)-c)@f)
    L=np.array([0.3,0.8,0.5]); L/=np.linalg.norm(L); sh=0.5+0.5*np.clip(n@L,0,1)
    for i in np.argsort(dep):
        if not vis[i]: continue
        q=[((p-c)@r*sc+S/2, S/2-(p-c)@u*sc) for p in P[i]]
        d.polygon(q,fill=tuple(int(min(255,x*sh[i])) for x in C[i]))
    return img
if __name__=='__main__':
    if len(sys.argv)>3: TEX=sys.argv[3]
    m=trimesh.load(sys.argv[1],force='mesh',process=False)
    if len(m.faces)>60000:
        idx=np.random.default_rng(0).choice(len(m.faces),60000,replace=False)
    else: idx=np.arange(len(m.faces))
    C=face_colors(m)[idx]; F=m.faces[idx]; V=m.vertices
    views={'+z':([1,0,0],[0,1,0],[0,0,1]),'-z':([-1,0,0],[0,1,0],[0,0,-1]),'+x':([0,0,-1],[0,1,0],[1,0,0]),'-x':([0,0,1],[0,1,0],[-1,0,0]),'+y':([1,0,0],[0,0,-1],[0,1,0]),'-y':([1,0,0],[0,0,1],[0,-1,0])}
    out=Image.new('RGB',(900,600))
    for k,(nm,v) in enumerate(views.items()):
        im=render(V,F,C,v); ImageDraw.Draw(im).text((5,5),'from '+nm,fill=(255,255,255)); out.paste(im,((k%3)*300,(k//3)*300))
    out.save(sys.argv[2])
