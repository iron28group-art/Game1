# 실행: python3 tools/sim/climb_preview.py <카메라높이m> <출력.png>
# ClimbGame.cs 의 배치 규칙으로 한 장면을 그려 보는 미리보기 (1080x1920, 1m = 100px)
import sys, math, random
sys.path.insert(0,'/home/user/Game1/tools/sim')
import importlib.util
spec=importlib.util.spec_from_file_location('cc','/home/user/Game1/tools/sim/climb_check.py')
from PIL import Image, ImageDraw, ImageFont
A='/home/user/Game1/Assets/Resources/Art/'
B={l.split()[0]:tuple(map(float,l.split()[1:])) for l in open(A+'art_bounds.txt') if l.strip() and l[0]!='#'}
def spr(i):
    im=Image.open(A+i+'.png').convert('RGBA'); w,h=im.size; x0,y0,x1,y1=B[i]
    return im.crop((math.floor(x0*w),math.floor((1-y1)*h),math.ceil(x1*w),math.ceil((1-y0)*h)))
SW,SH=1080,1920; PPU=100; camY=float(sys.argv[1]) if len(sys.argv)>1 else 12
img=Image.new('RGBA',(SW,SH),(255,219,204,255))
def to_px(x,y): return SW/2+x*PPU, SH/2-(y-camY)*PPU
def put(sp,cx,cy,w,h,flipx=False,flipy=False,alpha=255):
    s=sp.resize((max(1,int(w*PPU)),max(1,int(h*PPU))))
    if flipx: s=s.transpose(Image.FLIP_LEFT_RIGHT)
    if flipy: s=s.transpose(Image.FLIP_TOP_BOTTOM)
    px,py=to_px(cx,cy); img.alpha_composite(s,(int(px-s.width/2),int(py-s.height/2)))
# 배경 (시차 0.45, 홀수 조각 뒤집기)
bg=spr('bg_trunk_tile'); w=10.8+0.2; h=w/(bg.width/bg.height); local=camY*0.45; vh=19.2
first=math.floor((local-vh/2)/h)
for k in range(first,first+3): put(bg,0,camY+(k*h+h/2-local),w,h,flipy=(k&1)==1)
# 지형: 손으로 고른 예시 장면 (생성 규칙 범위 안의 값)
plats=[(0,0,11.8,True),(2.1,2.2,3.0,False),(-1.3,4.4,2.6,False),(1.9,6.7,2.4,False),(-2.2,8.9,2.8,False),(-1.6,13.3,2.6,False),(1.8,15.6,2.3,False),(-2.4,17.9,2.5,False),(4.0,8.7,1.9,False)]
vine=(-2.0,8.9,13.3)
branch=spr('obj_branch'); ba=branch.width/branch.height
vs=spr('obj_vine'); seg=0.75/(vs.width/vs.height); total=vine[2]-vine[1]+0.3; n=max(1,math.ceil(total/seg)); sh_=total/n
for i in range(n): put(vs,vine[0],vine[1]+sh_*(i+0.5),0.75,sh_+0.02,flipx=i&1)
for x,y,wd,cp in plats:
    if cp:
        piece=3.2; n=math.ceil((wd+0.6)/(piece*0.8)); st=-(n-1)*piece*0.8/2
        for i in range(n):
            hh=piece/ba; put(branch,st+i*piece*0.8,y-hh/2+hh*0.28,piece,hh,flipx=i&1)
        lp=spr('obj_firefly'); put(lp,-3.6,y+0.55,1.1*lp.width/lp.height,1.1)
    else:
        ww=wd+0.5; hh=ww/ba; put(branch,x,y-hh/2+hh*0.28,ww,hh)
def prop(i,x,y,width,flipx=False):
    s=spr(i); hh=width/(s.width/s.height); put(s,x,y+hh/2,width,hh,flipx)
prop('obj_mushroom',4.0,8.7,1.15)
prop('obj_caterpillar',1.6,6.7,1.0,flipx=True)
prop('obj_pinecone',-1.0,4.4,0.62)
for x,y in ((2.4,3.1),(4.0,9.6),(-1.4,14.2)): prop('icon_mana',x,y-0.3,0.6)
# 토리 (점프 중)
t=spr('tori_jump'); th=1.7; put(t,-0.2,5.6+th/2,th*t.width/t.height,th)
# UI
d=ImageDraw.Draw(img); F=lambda s:ImageFont.truetype('/usr/share/fonts/opentype/unifont/unifont.otf',s)
ov=Image.new('RGBA',img.size); ImageDraw.Draw(ov).rectangle([0,0,SW,190],fill=(92,61,77,90)); img.alpha_composite(ov)
def text(s,cx,cy,size,col=(255,255,255)):
    f=F(size); tw=d.textlength(s,font=f); d.text((cx-tw/2+2,cy-size/2+2),s,font=f,fill=(0,0,0,120)); d.text((cx-tw/2,cy-size/2),s,font=f,fill=col)
text('5 m',SW/2,75,76); text('최고 37 m   ·   뿌리 마을',SW/2,145,34,(255,235,191))
ic=spr('icon_mana').resize((48,70)); img.alpha_composite(ic,(SW-190-24,95-35)); d.text((SW-75-80,95-22),'12',font=F(44),fill='white')
def btn(cx,cy,size,label):
    ov=Image.new('RGBA',img.size); ImageDraw.Draw(ov).ellipse([cx-size/2,SH-cy-size/2,cx+size/2,SH-cy+size/2],fill=(255,242,230,90)); img.alpha_composite(ov)
    text(label,cx,SH-cy,int(size*0.32))
btn(150,170,190,'◀'); btn(360,170,190,'▶'); btn(SW-170,190,240,'점프'); btn(255,360,150,'▼'); btn(255,525,150,'▲')
img.convert('RGB').save(sys.argv[2] if len(sys.argv)>2 else '/tmp/sim/climb_preview.png')
