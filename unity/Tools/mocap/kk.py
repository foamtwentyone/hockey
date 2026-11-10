import numpy as np
from glb import GLB, quat2m
from PIL import Image
def from_to(a,b):
    a=a/np.linalg.norm(a); b=b/np.linalg.norm(b); v=np.cross(a,b); c=np.dot(a,b)
    if c<-0.9999:
        ax=np.cross(a,[1,0,0]);
        if np.linalg.norm(ax)<1e-3: ax=np.cross(a,[0,1,0])
        ax/=np.linalg.norm(ax); return 2*np.outer(ax,ax)-np.eye(3)
    vx=np.array([[0,-v[2],v[1]],[v[2],0,-v[0]],[-v[1],v[0],0]])
    return np.eye(3)+vx+vx@vx*(1/(1+c))
class KK:
    def __init__(s,path,texpath):
        g=GLB(path); s.g=g; N=g.j['nodes']; s.n=len(N)
        s.names=[x['name'] for x in N]; s.parent=[-1]*s.n
        for i,x in enumerate(N):
            for c in x.get('children',[]): s.parent[c]=i
        s.T=np.array([x.get('translation',[0,0,0]) for x in N],float)
        s.R0=[quat2m(x.get('rotation',[0,0,0,1])) for x in N]
        s.S=np.array([x.get('scale',[1,1,1]) for x in N],float)
        s.R=[r.copy() for r in s.R0]
        s.order=[]; seen=set()
        def visit(i):
            if i in seen: return
            if s.parent[i]>=0: visit(s.parent[i])
            seen.add(i); s.order.append(i)
        for i in range(s.n): visit(i)
        sk=g.j['skins'][0]; s.joints=sk['joints']; s.ibm=g.acc(sk['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
        s.meshes=[]
        for i,x in enumerate(N):
            if 'mesh' in x and 'skin' in x and x['name'] in ('Rogue_ArmLeft','Rogue_ArmRight','Rogue_Body','Rogue_Head','Rogue_LegLeft','Rogue_LegRight'):
                for pr in g.j['meshes'][x['mesh']]['primitives']:
                    a=pr['attributes']
                    s.meshes.append((g.acc(a['POSITION']),g.acc(a['JOINTS_0']).astype(int),g.acc(a['WEIGHTS_0']),g.acc(pr['indices']).astype(int).reshape(-1,3),g.acc(a['TEXCOORD_0'])))
        s.tex=np.array(Image.open(texpath).convert('RGB'))
        s.fk()
    def idx(s,name): return s.names.index(name)
    def fk(s):
        s.WR=[None]*s.n; s.WP=np.zeros((s.n,3)); s.WS=[None]*s.n
        for i in s.order:
            p=s.parent[i]; L=s.R[i]*s.S[i]
            if p<0: s.WR[i]=s.R[i]; s.WS[i]=L; s.WP[i]=s.T[i]
            else: s.WS[i]=s.WS[p]@L; s.WR[i]=s.WR[p]@s.R[i]; s.WP[i]=s.WP[p]+s.WS[p]@s.T[i]
    def set_world_rot(s,i,W):
        p=s.parent[i]; s.R[i]=(s.WR[p].T@W) if p>=0 else W
    def aim(s,bone,child,tgt):
        i,c=s.idx(bone),s.idx(child); s.fk()
        cur=s.WP[c]-s.WP[i]; M=from_to(cur,tgt)
        s.set_world_rot(i,M@s.WR[i]); s.fk()
    def twist(s,bone,axis_from,axis_to,cur_side,tgt_side):
        # повернуть кость вокруг её оси (axis) так, чтобы cur_side смотрел как tgt_side
        i=s.idx(bone); s.fk()
        ax=s.WP[s.idx(axis_to)]-s.WP[s.idx(axis_from)]; ax/=np.linalg.norm(ax)
        a=cur_side()-ax*np.dot(cur_side(),ax); b=tgt_side-ax*np.dot(tgt_side,ax)
        if np.linalg.norm(a)<1e-6 or np.linalg.norm(b)<1e-6: return
        M=from_to(a,b); s.set_world_rot(i,M@s.WR[i]); s.fk()
    def skin_tris(s):
        mats=[]
        for k,j in enumerate(s.joints):
            M=np.eye(4); M[:3,:3]=s.WS[j]; M[:3,3]=s.WP[j]; mats.append(M@s.ibm[k])
        mats=np.array(mats); out=[]
        for P,J,Wt,I,UV in s.meshes:
            ph=np.c_[P,np.ones(len(P))]
            V=np.zeros((len(P),3))
            for c in range(4): V+=Wt[:,c:c+1]*np.einsum('nij,nj->ni',mats[J[:,c]],ph)[:,:3]
            out.append((V,I,UV))
        return out
    def render(s,view,size=260,center=None,scale=None,lines=()):
        tris=s.skin_tris(); img=np.full((size,size,3),(38,40,58),np.uint8); zb=np.full((size,size),-1e9)
        # view: (right, up, toward camera) векторы
        r,u,f=[np.array(v,float) for v in view]
        allV=np.concatenate([t[0] for t in tris]); c=center if center is not None else allV.mean(0)
        sc=scale or size*0.42/ (np.ptp(allV@u)/2+1e-6)
        light=np.array([0.4,0.8,0.5]); light/=np.linalg.norm(light)
        from PIL import ImageDraw
        im=Image.fromarray(img); d=ImageDraw.Draw(im)
        polys=[]
        for V,I,UV in tris:
            for t in I:
                p=V[t]; n=np.cross(p[1]-p[0],p[2]-p[0]); nn=np.linalg.norm(n)
                if nn<1e-12: continue
                n/=nn
                if np.dot(n,f)<=0: continue
                uv=UV[t].mean(0); tx=s.tex[int(min(0.999,max(0,uv[1]))*(s.tex.shape[0]-1)), int(min(0.999,max(0,uv[0]))*(s.tex.shape[1]-1))]
                sh=0.55+0.45*max(0,np.dot(n,light))
                q=[((pp-c)@r*sc+size/2, size/2-(pp-c)@u*sc) for pp in p]
                polys.append(((p.mean(0)-c)@f, q, tuple(int(min(255,v*sh)) for v in tx)))
        polys.sort(key=lambda x:x[0])
        for _,q,col in polys: d.polygon(q,fill=col)
        for a,bb,col,w in lines:
            d.line([((a-c)@r*sc+size/2, size/2-(a-c)@u*sc),((bb-c)@r*sc+size/2, size/2-(bb-c)@u*sc)],fill=col,width=w)
        return im
