# ClimbWorld.cs 의 생성 규칙·점프 물리를 그대로 옮겨, 길 발판이 실제로 닿는지 시뮬레이션한다.
# 실행: python3 tools/sim/climb_check.py  (ClimbConfig/ClimbWorld 수치를 바꾸면 여기도 맞춰 주세요)
import random, math
HalfPlayWidth=4.9; BodyWidth=0.7; RunSpeed=5.2; AirControl=0.85; Gravity=-30; JumpSpeed=13; MaxFall=-18
BounceSpeed=18.5; RowGapMin=1.9; RowGapMax=2.45; RowGapHardBonus=0.15; PWmin=1.9; PWmax=3.4; VineGap=4.4; CPE=50
def diff(y): return min(1,max(0,y/300))
def reach(g,up):
    a=-Gravity; disc=up*up-2*a*g
    if disc<=0: return 0
    t=(up+math.sqrt(disc))/a; return t*RunSpeed*AirControl-0.25
class P:
    def __init__(s,x,y,w,cp=False): s.x,s.y,s.w,s.cp=x,y,w,cp
    l=property(lambda s:s.x-s.w/2); r=property(lambda s:s.x+s.w/2)
def gen(seed, top):
    rnd=random.Random(seed); R=lambda a,b:a+rnd.random()*(b-a); ch=lambda p:rnd.random()<p
    gap=lambda y:R(RowGapMin,RowGapMax)+RowGapHardBonus*diff(y)
    def place(b,w,rc):
        maxdx=max(0.8,rc+(w+b.w)/2); mindx=min(1.0,maxdx); d=1 if ch(.5) else -1; lim=HalfPlayWidth-w/2
        if b.x+d*mindx>lim or b.x+d*mindx<-lim: d=-d
        return max(-lim,min(lim,b.x+d*R(mindx,maxdx)))
    last=P(0,0,HalfPlayWidth*2+1,True); path=[('start',last)]; nextcp=CPE; ny=gap(0); since=0
    while ny<top:
        y=ny; d=diff(y)
        if y>=nextcp:
            last=P(0,nextcp,HalfPlayWidth*2+1,True); path.append(('cp',last)); ny=nextcp+gap(nextcp); nextcp+=CPE; since=0; continue
        if y>12 and since>=3 and ch(.14+.08*d) and y+VineGap<nextcp-1:
            yy=last.y+VineGap; w=R(2.2,3.0)
            if ch(.5):
                vx=max(last.l+.5,min(last.r-.5,last.x+R(-.3,.3)*last.w))
                ux=max(-HalfPlayWidth+w/2,min(HalfPlayWidth-w/2,vx+R(-.6,.6)*w)); ux=max(vx-w/2+.5,min(vx+w/2-.5,ux))
                last=P(ux,yy,w); path.append(('vine',last,vx))
            else:
                mx=max(last.l+.5,min(last.r-.5,last.x+R(-.3,.3)*last.w))
                last=P(place(P(mx,last.y,.6),w,reach(VineGap,BounceSpeed)),yy,w); path.append(('mush',last,mx))
            since=0; ny=yy+gap(yy); continue
        g=y-last.y; w=max(PWmin,min(PWmax,(PWmax+(PWmin+.2-PWmax)*d)*R(.8,1.1)))
        x=place(last,w,reach(g,JumpSpeed)); p=P(x,y,w)
        if ch(.3): R(1.6,2.2); R(3.2,4.5); R(-.3,.3)   # 보너스 발판 난수 소비(순서 맞추기용, 결과엔 무관)
        ch(.45)
        last=p; path.append(('row',p)); since+=1; ny=y+gap(y)
    return path
def can_jump(a,b,up=JumpSpeed,start_x=None):
    """a 위의 여러 출발점에서 b 쪽으로 방향키를 누른 채 점프 → b 에 착지하는지 (dt=1/60 물리)."""
    xs=[start_x] if start_x is not None else [a.l+0.36+i*(a.w-0.72)/8 for i in range(9)]
    for sx in xs:
        for hold_until in (99, 0.35, 0.2):           # 계속 누르기 / 중간에 떼기
            x,y,vx,vy=sx,a.y,0,up; t=0; dirn=1 if b.x>sx else -1
            while t<3:
                dt=1/60; t+=dt
                h=dirn if t<hold_until else 0
                vx=h*RunSpeed*AirControl; vy=max(vy+Gravity*dt,MaxFall); py=y; x+=vx*dt; y+=vy*dt
                x=max(-HalfPlayWidth,min(HalfPlayWidth,x))
                if vy<=0 and py>=b.y-0.001 and y<=b.y and x+BodyWidth/2>b.l and x-BodyWidth/2<b.r: return True
                if vy<=0 and y<b.y-3: break
    return False
bad=0; total=0; worst=0
for seed in range(1,301):
    path=gen(seed,400)
    for i in range(1,len(path)):
        kind=path[i][0]; a=path[i-1][1]; b=path[i][1]
        if kind=='cp': continue        # 화면 폭 전체 → 항상 닿음
        total+=1
        if kind=='row': ok=can_jump(a,b)
        elif kind=='mush': ok=can_jump(a,b,BounceSpeed,start_x=path[i][2])
        else: ok=True                  # 덩굴: 위 발판이 덩굴 x 를 덮음 (생성 규칙으로 보장) 
        if kind=='vine': ok = b.l<=path[i][2]<=b.r
        if not ok:
            bad+=1
            if bad<=5: print('닿지 않음', seed, kind, f'a=({a.x:.2f},{a.y:.2f},w{a.w:.2f}) b=({b.x:.2f},{b.y:.2f},w{b.w:.2f}) gap={b.y-a.y:.2f}')
print(f'검사 {total}칸, 닿지 않는 칸 {bad}')
