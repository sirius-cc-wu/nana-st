-work

\ ===== SFC version =====
: sfc-ver
  ." SFC-version|1.0.1" cr
;

\ ===== SFC configuration commands =====
\ 這裡增加的命令是為了避免專案中的 SFC 造成 botnana 測試時沒接 slave , motion server 就跑不起來的困擾。

variable PECM-configured
PECM-configured on

\ 若 alias 不存在就採用給定的 slave number
: config-slave ( slave alias -- ) ( input duff: name )
  dup ec-alias?
  if
    swap drop ec-a>n
  else
    drop PECM-configured off
  then
  create , does> @
;

\ 用於 SFC 載入完成後，要啟動最初的 step，若 PECM slaves config 不成功，就不啟動。
: start-sfc ( xt -- )
  PECM-configured @
  if
    +step
  else
    drop
  then
;



\ ===== Global variables =====
0 constant idle
1 constant jogging
2 constant machining
3 constant meas

variable system-ready
variable PECM-going             \ GoPECM 工作/休眠旗標
variable GoECM-in-idle          \ Go ECM in idle 
GoECM-in-idle on
variable PECM-power-state        \ power on/off 狀態
variable PECM-motion-state       \ < 0(idle), 1(jogging), 2(machining), 3(meas) >
\ groups
1 constant meas-group



\ ===== EtherCAT Slave configurations =====
\ 這裡為 PECM 的 Slave, channel 定義和全域變數的配置檔

\ ----- Drives -----

\ ----- Digital inputs -----

2 101 config-slave el1809-1
3 102 config-slave el1809-2
4 103 config-slave el1809-3

\ EL1809-1
1  el1809-1 2constant IPC-ems-din               \ Emergency Button
2  el1809-1 2constant ew-ems-din                \ E-Water Emergency Button
3  el1809-1 2constant 3phase-din                \ 三相檢知器
4  el1809-1 2constant sys-off-din               \ System off Button
5  el1809-1 2constant process-valve-din         \ Process valve 致能
6  el1809-1 2constant outside-pressure-din      \ 外部氣壓壓力源檢知
7  el1809-1 2constant HNO3-level-low-din        \ 硝酸液位下限檢知
8  el1809-1 2constant HNO3-level-toolow-din     \ 硝酸液位過低檢知
9  el1809-1 2constant NaOH-level-low-din        \ 氫氧化鈉液位下限檢知
10 el1809-1 2constant NaOH-level-toolow-din     \ 氫氧化鈉液位過低檢知
11 el1809-1 2constant FeSO4-level-low-din       \ 硫酸亞鐵液位下限檢知
12 el1809-1 2constant FeSO4-level-toolow-din    \ 硫酸亞鐵液位過低檢知
13 el1809-1 2constant aqua-bt-upper-din         \ 緩衝槽位上限檢知
14 el1809-1 2constant aqua-bt-too-upper-din     \ 緩衝槽位過高檢知
15 el1809-1 2constant aqua-bt-too-lower-din     \ 緩衝槽位過低檢知
16 el1809-1 2constant aqua-bt-lower-din         \ 緩衝槽位下限檢知

\ EL1809-2
1  el1809-2 2constant reactor-PH-err-din        \ 反應槽的PH meter 異常
2  el1809-2 2constant dosing-level-high-din     \ Dosing pump的漏液收集瓶 液位過高
3  el1809-2 2constant NaCl-pump-low-din         \ 鹽水槽的液位過低檢知
4  el1809-2 2constant NaCl-pump-high-din        \ 鹽水槽的液位過高檢知
5  el1809-2 2constant walkway-leaking-din       \ 走道漏水檢知
6  el1809-2 2constant IW-leaking-din            \ 冰水機三通閥的漏水檢知
7  el1809-2 2constant elect-leaking-din         \ 電解液處理的托盤的漏水檢知
8  el1809-2 2constant buffer-leaking-din        \ Buffer tank漏水承接盤的漏水檢知
9  el1809-2 2constant filter-pump-din           \ Filter inlet pump ready
10 el1809-2 2constant backwash-leaking-din      \ 過濾逆洗裝置漏水承接盤的漏水檢知
11 el1809-2 2constant M123-leaking-din          \ M1 M2 M3漏水承接盤的漏水檢知
12 el1809-2 2constant reactor-level-low-din     \ 反應槽液位下限檢知
13 el1809-2 2constant mid-under-leaking-din     \ 中央台下方漏水檢知
14 el1809-2 2constant mid0-leaking-din          \ 中央台排水道0度漏水檢知
15 el1809-2 2constant mid180-leaking-din        \ 中央台排水道180度漏水檢知

\ EL1809-3
1  el1809-3 2constant group1-overload-din       \ Group 1 所有電磁開關過載的集合輸入
2  el1809-3 2constant group2-overload-din       \ Group 2 所有電磁開關過載的集合輸入
3  el1809-3 2constant filer2buffer-high-din     \ 壓濾機中繼緩衝槽的高液位檢知
4  el1809-3 2constant filer2buffer-toohigh-din  \ 壓濾機中繼緩衝槽的過高液位檢知
5  el1809-3 2constant NaN03-err-din             \ 硝酸鈉加藥幫浦異常
6  el1809-3 2constant FeS-err-din               \ 硫酸亞鐵加藥幫浦異常
7  el1809-3 2constant NaOH-err-din              \ 氫氧化鈉加藥幫浦異常
8  el1809-3 2constant HNO3-err-din              \ 硝酸加藥幫浦異常
9  el1809-3 2constant shimizu-low-lev-din       \ 清水槽低液位檢知
10 el1809-3 2constant shimizu-toolow-lev-din    \ 清水槽過低液位檢知
11 el1809-3 2constant filer2buffer-low-din      \ 壓濾機中繼緩衝槽的低液位檢知
12 el1809-3 2constant filer2buffer-toolow-din   \ 壓濾機中繼緩衝槽的過低液位檢知
13 el1809-3 2constant cr6-level-high-din        \ 六價鉻桶高液位檢知
14 el1809-3 2constant cr6-level-low-din         \ 六價鉻桶低液位檢知
15 el1809-3 2constant cr6-onReady-din           \ 六價鉻系統準備通知
\ ----- Digital outputs -----

8 107 config-slave el2809-1
10 109 config-slave el2809-2
11 110 config-slave el2809-3

\ EL2809-1
1  el2809-1 2constant y-led-dout                \ electoryte processing plant ready
2  el2809-1 2constant r-led-dout                \ electoryte processing plant falut
3  el2809-1 2constant g-led-dout
4  el2809-1 2constant sys-off-dout              \ System off
6  el2809-1 2constant m2-pump-valve             \ m2 pump release valve
7  el2809-1 2constant m4-pump-valve             \ drirty water output of inlet 進端廢水輸出閥
16 el2809-1 2constant buzzer-dout               \ 蜂鳴器

\ EL2809-2
1  el2809-2 2constant reactor-sw-valve-dout       \ 反應槽供水閥
2  el2809-2 2constant NaCl-sw-valve-dout          \ 鹽水槽供水閥
3  el2809-2 2constant reactor-pump-dout           \ 反應槽氣動幫浦的氣動閥
4  el2809-2 2constant press-inw-valve-dout        \ 壓濾機入水槽
5  el2809-2 2constant reactor-inw-valve-dout      \ 反應槽入水閥
6  el2809-2 2constant pre-inw-valve-dout          \ 沈澱槽入水閥
7  el2809-2 2constant outside-air-valve-doit      \ 外部氣壓源輸入控制閥
8  el2809-2 2constant press-bottom-pump-dout      \ 控制壓濾機艙底污水幫浦
10 el2809-2 2constant aqua-bt-pump-dout           \ 控制緩衝槽抽水幫浦
11 el2809-2 2constant meter-pw-dout               \ 控制加藥機 導電度表 酸鹼度表電源
14 el2809-2 2constant ew-ready-dout               \ e-water-ready
15 el2809-2 2constant chiller-dout                \ 冰水機閥
16 el2809-2 2constant shimizu-valve-dout          \ 清水槽溫控閥


\ EL2809-3
\ 共分為M1 M2 M3 M4
\ 其中M1 為主控箱開啟水進到加工槽開關
\ M2 為入水, M3 為循環, M4 為逆洗
\ M3常態開啟, M2與M4交錯開啟
1  el2809-3 2constant m2-in-valve-dout            \ 過濾補水閥
2  el2809-3 2constant m2-out-valve-dout           \ 過濾後清潔電解液輸入閥
3  el2809-3 2constant m4-out-valve-dout           \ 逆洗閥
4  el2809-3 2constant pre-input-valve-dout        \ 沈澱槽輸入閥 EA
5  el2809-3 2constant cr6-input-valve-dout        \ 六價鉻處理槽進水閥 EB
6  el2809-3 2constant m2-pump-dout                \ 過濾輸入幫浦 G2_-11M2的變頻器
7  el2809-3 2constant m3-pump-dout                \ 過濾循環幫浦 G2_-12M3
8  el2809-3 2constant m4-pump-dout                \ 過濾逆洗幫浦 G2_-12M4
9  el2809-3 2constant clean-pump-dout             \ 控制機台護罩清理氣動幫浦的電磁閥
10 el2809-3 2constant clean-nozzle-dout           \ 控制機台護罩清理噴嘴的電磁閥
11 el2809-3 2constant m11-pump-dout               \ 控制清水槽的冰水機內循環幫浦 G2_-12M11
13 el2809-3 2constant sa-valve-dout               \ 工作站a的加工液輸入閥
14 el2809-3 2constant sb-valve-dout               \ 工作站b的加工液輸入閥
15 el2809-3 2constant sc-valve-dout               \ 工作站c的加工液輸入閥
16 el2809-3 2constant m4-in-valve-dout            \ 逆洗後前端排水閥
\ ----- Analog inputs -----

5 104 config-slave el3058-1
6 105 config-slave el3058-2

\ el3058-1
1  el3058-1 2constant reactor-conductivit-ain     \ 反應槽的水導電度
2  el3058-1 2constant reactor-ph-ain              \ 反應槽的ph meter
3  el3058-1 2constant NaCl-conductivit-ain        \ 鹽水槽的水導電度
4  el3058-1 2constant dirty-water-level-ain       \ 髒水槽水位檢測
5  el3058-1 2constant cr6-conductivit-ain         \ 六價鉻分析儀的檢測輸出
7  el3058-1 2constant m1-flow                     \ 加工液幫浦(G2_-11M1)輸出的水流量檢知
8  el3058-1 2constant m1-pressure                 \ 加工液幫浦(G2_-11M1)輸出的水壓檢知

\ el3058-2
1  el3058-2 2constant filter-out-flow             \ 過濾後的流量檢知
2  el3058-2 2constant m3-flow                     \ 過濾循環幫浦 (G2_-12M3)的流量檢知
3  el3058-2 2constant filter-in-flow              \ 過濾前的流量檢知
4  el3058-2 2constant filter-out-pressure         \ 過濾後的水壓檢知
5  el3058-2 2constant shimizu-level               \ 清水槽的液位檢知
7  el3058-2 2constant shimizu-ph-ain              \ 清水槽的ph meter
8  el3058-2 2constant shimizu-tmp-b               \ 清水槽的液溫

\ ----- Analog outputs -----

7 106 config-slave el4002
9 108 config-slave el4024

\ el4002
1 el4002 2constant m2-pump-aout                   \ 過濾幫浦(G2_-11M2)的轉速電壓

\ EL4024
1 el4024 2constant NaN03-pump-aout                \ 硫酸鈉的dosing pump加藥訊號輸出
2 el4024 2constant FeSO4-pump-aout                \ 硫化亞鐵的dosing pump加藥訊號輸出
3 el4024 2constant NaOH-pump-aout                 \ 氫氧化鈉的dosing pump加藥訊號輸出
4 el4024 2constant HNO3-pump-aout                 \ 硝酸的dosing pump加藥訊號輸出

\ ----- I/O Actions -----

\ Dout 輸出指令
: +dout ( channel slave -- ) 1 -rot ec-dout! ;
: -dout ( channel slave -- ) 0 -rot ec-dout! ;

\ 三相檢知
: 3phase@ ( -- flag ) 3phase-din ec-din@ ;

\ Button
: sys-off-btn@ ( -- flag ) sys-off-din ec-din@ ;
: is-IPC-EMS?  ( -- flag ) IPC-ems-din ec-din@ not ;      \ 緊急停止訊號為反向
: is-EW-EMS?  ( -- flag ) ew-ems-din ec-din@ not ;        \ 緊急停止訊號為反向

\ system off output
: +sys-off ( -- ) sys-off-dout +dout ;

\ 反應槽攪拌
: +reactor-pump
  reactor-pump-dout +dout
;
: -reactor-pump
  reactor-pump-dout -dout
;

\ 由使用者判斷是否使EAEB
variable useEB
: +useEB
  useEB on
;
: -useEB
  useEB off
;

\ 當開啟使用旗標 並且六價鉻準備好且並沒有碰到高水位時啟動 選用EB,其餘狀況選用EA
: EA/EB?
  useEB @
  cr6-onReady-din ec-din@ and
  cr6-level-high-din ec-din@ not and
  if
    \ 選用EB
    cr6-input-valve-dout
  else
    \ 選用EA
    pre-input-valve-dout
  then

;
\ Buftank
: aqua-bt-upper@ ( -- flag ) aqua-bt-upper-din ec-din@ ;
: aqua-bt-lower@ ( -- flag ) aqua-bt-lower-din ec-din@ ;
: +aqua-bt-pump ( -- ) aqua-bt-pump-dout +dout ;
: -aqua-bt-pump ( -- ) aqua-bt-pump-dout -dout ;

: compressor-bt-upper@ ( -- flag ) filer2buffer-high-din ec-din@ ;
: compressor-bt-lower@ ( -- flag ) filer2buffer-low-din ec-din@ ;
: +compressor-bt-pump ( -- ) press-bottom-pump-dout +dout ;
: -compressor-bt-pump ( -- ) press-bottom-pump-dout -dout ;

\ shimizu
: shimizu-low@ ( -- flag ) shimizu-low-lev-din ec-din@ ;
: shimizu-toolow@ ( -- flag ) shimizu-toolow-lev-din ec-din@ ; 

\ 蜂鳴器
: +buzzer ( -- ) buzzer-dout +dout ;
: -buzzer ( -- ) buzzer-dout -dout ;

\ 三色燈
: +g-led ( -- )
  g-led-dout +dout
  y-led-dout -dout
  r-led-dout -dout
;
: +y-led ( -- )
  g-led-dout -dout
  y-led-dout +dout
  r-led-dout -dout
;
: +r-led ( -- )
  g-led-dout -dout
  y-led-dout -dout
  r-led-dout +dout
;
: +all-led ( -- )
  g-led-dout +dout
  y-led-dout +dout
  r-led-dout +dout
;
: -all-led ( -- )
  g-led-dout -dout
  y-led-dout -dout
  r-led-dout -dout
;
\ system ready?
: system-ready? ( -- flag )
  system-ready @
;

\ System Button variable
variable $sys-btn1
variable $sys-btn2
variable $sys-btn3
variable $sys-btn4
variable $sys-off-state FALSE $sys-off-state !
: sys-off-btn?
  $sys-btn3 @ $sys-btn4 !
  $sys-btn2 @ $sys-btn3 !
  $sys-btn1 @ $sys-btn2 !
  sys-off-btn@ 0<> $sys-btn1 !
  $sys-btn1 @ $sys-btn2 @ $sys-btn3 @ not $sys-btn4 @ not and and and $sys-off-state !
;
: check-sys-off? ( -- flag)
  $sys-off-state @ 0= if
    sys-off-btn?
  then
  $sys-off-state @
;

marker -work