# 平衡巡检基线（R218）——12 种子 × 4 策略 × 720s

> 数据来源：tests/sim/bot.ts（确定性 bot，非人类水平标杆——只能证伪明显退化，
> 不能证平衡完美）。复测方法：临时 vitest 跑 runBot(seed, policy, 60*720) 聚合，
> 与本文件 diff 即得回归/改进方向。

## 聚合结论（R176 手感转向后的首次全量复测）

| 策略 | 雪球（720s 存活） | 死亡局 | 中位死亡时长 | 判读 |
|---|---|---|---|---|
| orbit（绕圈） | 3/12（s5/s11/s12） | 9 | ~38s | exploiting 策略被压（R187b 时代 ~5/12）✓ |
| seek（追打） | 3/12（s5/s7/s8） | 9 | ~32s | s5 大雪球（2626 杀/阶 12/2 妖王）属种子方差 |
| stick（站桩） | 5/12（s1/s4/s6/s7/s8） | 7 | ~74s | 存活较 R187b（中位 44-65s）**提升**——站桩不送死，合理 |
| afk（挂机） | 0/12 | 12（18-20s 全灭、0 杀） | ~19s | 防挂机健康 ✓ |

**判读**：漏斗形曲线成立——移动型 bot 第一幕 26-51s 死亡（dumb 策略挡不住
妖潮，符合「第一幕温和但不无害」），存活者滚雪球进后期。R176 以来的密度/
敌速/热身/保底调优在数据上站住，无回归、无需调参。

## 明细（48 行）

    orbit s1: t=38s state=GameOver kills=22 round=1 act=0 score=225 combo=11 elite=1 boss=0 cause=游魂
    orbit s2: t=31s state=GameOver kills=14 round=1 act=0 score=140 combo=7 elite=0 boss=0 cause=游魂
    orbit s3: t=44s state=GameOver kills=25 round=1 act=0 score=250 combo=10 elite=0 boss=0 cause=游魂
    orbit s4: t=39s state=GameOver kills=19 round=1 act=0 score=190 combo=20 elite=0 boss=0 cause=游魂
    orbit s5: t=720s state=Combat kills=989 round=6 act=1 score=14375 combo=191 elite=120 boss=1 cause=-
    orbit s6: t=38s state=GameOver kills=16 round=1 act=0 score=160 combo=8 elite=0 boss=0 cause=游魂
    orbit s7: t=34s state=GameOver kills=17 round=1 act=0 score=170 combo=5 elite=0 boss=0 cause=游魂
    orbit s8: t=51s state=GameOver kills=52 round=2 act=0 score=545 combo=43 elite=1 boss=0 cause=游魂
    orbit s9: t=40s state=GameOver kills=13 round=1 act=0 score=130 combo=5 elite=0 boss=0 cause=精英游魂
    orbit s10: t=37s state=GameOver kills=21 round=1 act=0 score=210 combo=17 elite=0 boss=0 cause=游魂
    orbit s11: t=720s state=Combat kills=295 round=4 act=0 score=3200 combo=57 elite=15 boss=0 cause=-
    orbit s12: t=720s state=Combat kills=1382 round=8 act=2 score=22040 combo=39 elite=80 boss=1 cause=-
    seek s1: t=37s state=GameOver kills=24 round=1 act=0 score=240 combo=30 elite=0 boss=0 cause=游魂
    seek s2: t=31s state=GameOver kills=22 round=1 act=0 score=220 combo=14 elite=0 boss=0 cause=游魂
    seek s3: t=31s state=GameOver kills=19 round=1 act=0 score=190 combo=14 elite=0 boss=0 cause=游魂
    seek s4: t=26s state=GameOver kills=22 round=1 act=0 score=225 combo=27 elite=1 boss=0 cause=游魂
    seek s5: t=720s state=Combat kills=2626 round=12 act=3 score=54136 combo=163 elite=252 boss=2 cause=-
    seek s6: t=32s state=GameOver kills=22 round=1 act=0 score=220 combo=24 elite=0 boss=0 cause=游魂
    seek s7: t=720s state=Combat kills=652 round=5 act=1 score=7999 combo=31 elite=65 boss=1 cause=-
    seek s8: t=720s state=Combat kills=146 round=2 act=0 score=1575 combo=48 elite=11 boss=0 cause=-
    seek s9: t=35s state=GameOver kills=26 round=1 act=0 score=260 combo=11 elite=0 boss=0 cause=游魂
    seek s10: t=31s state=GameOver kills=18 round=1 act=0 score=180 combo=16 elite=0 boss=0 cause=游魂
    seek s11: t=43s state=GameOver kills=30 round=1 act=0 score=300 combo=29 elite=0 boss=0 cause=游魂
    seek s12: t=34s state=GameOver kills=22 round=1 act=0 score=220 combo=35 elite=0 boss=0 cause=游魂
    stick s1: t=720s state=Combat kills=108 round=2 act=0 score=1115 combo=11 elite=3 boss=0 cause=-
    stick s2: t=160s state=GameOver kills=17 round=4 act=1 score=185 combo=24 elite=0 boss=0 cause=游魂
    stick s3: t=153s state=GameOver kills=21 round=4 act=1 score=215 combo=24 elite=0 boss=0 cause=魅影
    stick s4: t=720s state=Combat kills=249 round=3 act=0 score=2620 combo=12 elite=4 boss=0 cause=-
    stick s5: t=74s state=GameOver kills=49 round=2 act=0 score=495 combo=56 elite=1 boss=0 cause=游魂
    stick s6: t=720s state=Combat kills=414 round=4 act=1 score=4950 combo=199 elite=41 boss=0 cause=-
    stick s7: t=720s state=Combat kills=106 round=3 act=0 score=1212 combo=8 elite=4 boss=0 cause=-
    stick s8: t=720s state=Combat kills=22 round=3 act=0 score=220 combo=17 elite=0 boss=0 cause=-
    stick s9: t=57s state=GameOver kills=25 round=2 act=0 score=250 combo=25 elite=0 boss=0 cause=游魂
    stick s10: t=46s state=GameOver kills=16 round=2 act=0 score=160 combo=8 elite=0 boss=0 cause=游魂
    stick s11: t=198s state=GameOver kills=100 round=5 act=1 score=1171 combo=18 elite=6 boss=0 cause=游魂
    stick s12: t=52s state=GameOver kills=19 round=2 act=0 score=190 combo=6 elite=0 boss=0 cause=游魂
    afk s1-s12: t=18-20s 全灭、kills=0、cause=游魂（防挂机健康）

## 复测指引

跑法（临时 vitest，勿入库）：

    // tests/sim/sweep-diag.test.ts
    import { runBot } from "./bot";
    it("sweep", () => { for (const p of ["orbit","seek","stick","afk"]) for (let s=1;s<=12;s++) console.log(p, s, JSON.stringify(runBot(s, p))); }, 900000);

与本文「明细」逐行 diff：死亡时长中位数漂移 >30%、或雪球数 ±3 以上，
即为曲线回归/改进信号，进入调参讨论。
