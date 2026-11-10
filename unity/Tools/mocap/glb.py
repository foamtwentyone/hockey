import json, struct, numpy as np
def quat2m(q):
    x,y,z,w=q
    return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
class GLB:
    def __init__(s,path):
        b=open(path,'rb').read()
        n=struct.unpack('<I',b[12:16])[0]; s.j=json.loads(b[20:20+n]); o=20+n
        m=struct.unpack('<I',b[o:o+4])[0]; s.bin=b[o+8:o+8+m]
    def acc(s,i):
        a=s.j['accessors'][i]; bv=s.j['bufferViews'][a['bufferView']]
        ct={5126:('f',4),5123:('H',2),5125:('I',4),5121:('B',1)}[a['componentType']]
        nc={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
        off=bv.get('byteOffset',0)+a.get('byteOffset',0); stride=bv.get('byteStride',ct[1]*nc)
        arr=np.zeros((a['count'],nc))
        for k in range(a['count']):
            arr[k]=struct.unpack_from('<'+ct[0]*nc,s.bin,off+k*stride)
        if a.get('normalized') and ct[0]=='B': arr/=255
        return arr
