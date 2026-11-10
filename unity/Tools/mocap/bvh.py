import numpy as np, re
def rotm(axis, deg):
    a=np.radians(deg); c,s=np.cos(a),np.sin(a)
    if axis=='X': return np.array([[1,0,0],[0,c,-s],[0,s,c]])
    if axis=='Y': return np.array([[c,0,s],[0,1,0],[-s,0,c]])
    return np.array([[c,-s,0],[s,c,0],[0,0,1]])
class BVH:
    def __init__(s, path):
        toks=open(path).read().split()
        s.names=[];s.parent=[];s.offset=[];s.chans=[]
        i=0; stack=[]
        while toks[i]!='MOTION':
            t=toks[i]
            if t in('ROOT','JOINT'):
                s.names.append(toks[i+1]); s.parent.append(stack[-1] if stack else -1); s.chans.append([]); s.offset.append(None); cur=len(s.names)-1; i+=2
            elif t=='End':
                s.names.append(s.names[stack[-1]]+'_end'); s.parent.append(stack[-1]); s.chans.append([]); s.offset.append(None); cur=len(s.names)-1; i+=2
            elif t=='{': stack.append(cur); i+=1
            elif t=='}': stack.pop(); i+=1
            elif t=='OFFSET': s.offset[cur]=np.array(list(map(float,toks[i+1:i+4]))); i+=4
            elif t=='CHANNELS':
                n=int(toks[i+1]); s.chans[cur]=toks[i+2:i+2+n]; i+=2+n
            else: i+=1
        i+=1; nf=int(toks[i+1]); s.dt=float(toks[i+4]); i+=5
        tot=sum(len(c) for c in s.chans)
        s.data=np.array(list(map(float,toks[i:i+nf*tot]))).reshape(nf,tot)
        s.offset=np.array(s.offset)
    def fk(s, f):
        row=s.data[f]; k=0; n=len(s.names)
        R=[None]*n; P=np.zeros((n,3))
        for j in range(n):
            loc=np.eye(3); t=s.offset[j].copy()
            for c in s.chans[j]:
                v=row[k]; k+=1
                if c.endswith('position'): t['XYZ'.index(c[0])]=v
                else: loc=loc@rotm(c[0],v)
            p=s.parent[j]
            if p<0: R[j]=loc; P[j]=t
            else: R[j]=R[p]@loc; P[j]=P[p]+R[p]@t
        return P,R
    def idx(s,n): return s.names.index(n)
