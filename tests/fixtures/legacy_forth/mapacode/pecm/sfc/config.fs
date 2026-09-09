-work

\ ===== SFC version =====
: sfc-ver
  ." SFC-version|1.0.1" cr
;

\ ===== SFC configuration commands =====
\ 這裡增加的命令是為了避免專案中的 SFC 造成 botnana 測試時沒接 slave , motion server 就跑不起來的困擾。

variable PECM-configured
PECM-configured on
variable munk-configured
munk-configured on

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

: munk-config-slave ( slave alias -- ) ( input duff: name )
  dup ec-alias?
  if
    swap drop ec-a>n
  else
    drop munk-configured off
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
variable GoECM-fault            \ 放電加工異警旗標
variable PECM-power-state        \ power on/off 狀態
variable PECM-motion-state       \ < 0(idle), 1(jogging), 2(machining), 3(meas) >
variable PECM-user-axis 1 PECM-user-axis !
\ groups
1 constant meas-group



\ ===== EtherCAT Slave configurations =====
\ 這裡為 PECM 的 Slave, channel 定義和全域變數的配置檔

\ ----- Drives -----

14 1 config-slave slave-xa               \ XA slave number
1 slave-xa 2constant drive-xa            \ XA channel and slave number
1 constant PECM-XA-axis                  \ XA axis number

15 2 config-slave slave-xb               \ XB slave number
1 slave-xb 2constant drive-xb            \ XB channel and slave number
2 constant PECM-XB-axis                  \ XB axis number

16 3 config-slave slave-xc               \ XC slave number
1 slave-xc 2constant drive-xc            \ XC channel and slave number
3 constant PECM-XC-axis                  \ XC axis number

17 4 config-slave slave-w                \ W  slave number
1 slave-w 2constant drive-w              \ W  channel and slave number
4 constant PECM-W-axis                   \ W  axis number

18 5 config-slave slave-dl               \ DL slave number
1 slave-dl 2constant drive-dl            \ DL channel and slave number
5 constant PECM-DL-axis                  \ DL axis number

19 6 config-slave slave-dr               \ DR slave number
1 slave-dr 2constant drive-dr            \ DR channel and slave number
6 constant PECM-DR-axis                  \ DR axis number

\ AnyBus
20 201 munk-config-slave AnyBus                 \ AnyBus
1 AnyBus 2constant gateway-ch-slv
\ ----- Digital inputs -----

2 102 config-slave el1819
3 103 config-slave el1809

\ EL1819
1  el1819 2constant ems-din           \ Emergency Button
2  el1819 2constant sys-off-din       \ System off Button
3  el1819 2constant 3phase-din        \ 三相檢知器
4  el1819 2constant pw-btn-din        \ power on/off Button
5  el1819 2constant pause-ch-slv      \ pause channel and slave number
6  el1819 2constant reset-ch-slv      \ reset channel and slave number
9  el1819 2constant start-ch-slv      \ start channel and slave number
12 el1819 2constant aqua-ems-din      \ EMS of Aqua
13 el1819 2constant aqua-ready-din    \ Aqua Ready
14 el1819 2constant aqua-pump-running-din \ Puckmeldung Pumpe (Pump running)
15 el1819 2constant ECM-done-ch-slv   \ ECM_done channel and slave number
16 el1819 2constant short-alarm-ch-slv  \ short-alarm channel and slave number

\ EL1809
1  el1809 2constant platform-door-din \ 加工平台安全門
3  el1809 2constant xa-chuck-din      \ xa_夾頭吹氣壓力感知
4  el1809 2constant xb-chuck-din      \ xb_夾頭吹氣壓力感知
5  el1809 2constant xc-chuck-din      \ xc_夾頭吹氣壓力感知
6  el1809 2constant tbar-chuck-din    \ Tbar_夾頭吹氣壓力感知
7  el1809 2constant clamp-din
8  el1809 2constant unclamp-din
9  el1809 2constant zero-return       \ 旋轉軸原點
13 el1809 2constant left-Float-din    \ 排水溝左浮球
14 el1809 2constant right-Float-din   \ 排水溝右浮球
16 el1809 2constant touch-din         \ touch訊號


\ ----- Digital outputs -----

4 104 config-slave el2809-1
5 105 config-slave el2809-2
10 110 config-slave el2808-1
11 111 config-slave el2809-3
12 112 config-slave el2809-4

\ EL2809-1
1  el2809-1 2constant sys-off-dout      \ System off
2  el2809-1 2constant buzzer-dout       \ 蜂鳴器
3  el2809-1 2constant g-led-dout        \ Ready(綠燈)
4  el2809-1 2constant y-led-dout        \ Warning(黃燈)
5  el2809-1 2constant r-led-dout        \ Error(紅燈)
6  el2809-1 2constant prcs-pump-run-dout    \ Process pump run
7  el2809-1 2constant prcs-valve-dout       \ Process valve
8  el2809-1 2constant rls-elect-dout        \ Release electrolyte
11 el2809-1 2constant pw-led-dout       \ power on/off LED
12 el2809-1 2constant pause-LED-ch-slv  \ pause LED channel and slave number
13 el2809-1 2constant start-LED-ch-slv  \ start LED channel and slave number
14 el2809-1 2constant cnc-led-dout      \ CNC 備妥燈
15 el2809-1 2constant pw-supply-dout    \ 驅動器電源供應

\ EL2809-2
1  el2809-2 2constant xa-lock-dout
2  el2809-2 2constant xa-blow-dout
3  el2809-2 2constant xa-check-dout
4  el2809-2 2constant xb-lock-dout
5  el2809-2 2constant xb-blow-dout
6  el2809-2 2constant xb-check-dout
7  el2809-2 2constant xc-lock-dout
8  el2809-2 2constant xc-blow-dout
9  el2809-2 2constant xc-check-dout
10 el2809-2 2constant Go-ECM-dout
11 el2809-2 2constant 3V-control-dout   \ DSP module
12 el2809-2 2constant tbar-lock-dout
13 el2809-2 2constant tbar-blow-dout
14 el2809-2 2constant tbar-check-dout
16 el2809-2 2constant 5v-control-dout

\ EL2809-3

1  el2809-3 2constant xa-water-dout
2  el2809-3 2constant xb-water-dout
3  el2809-3 2constant xc-water-dout
4  el2809-3 2constant vi-trigger

5  el2809-3 2constant xc-chuck-lock      \ xc-chuck-lock
6  el2809-3 2constant xc-chuck-release   \ xc-chuck-release
7  el2809-3 2constant xc-chuck-blow      \ xc-chuck-blow
8  el2809-3 2constant xc-chuck-check     \ xc-chuck-check

\ EL2808-1

1  el2808-1 2constant xa-chuck-lock      \ xa-chuck-lock
2  el2808-1 2constant xa-chuck-release   \ xa-chuck-release
3  el2808-1 2constant xa-chuck-blow      \ xa-chuck-blow
4  el2808-1 2constant xa-chuck-check     \ xa-chuck-check
5  el2808-1 2constant xb-chuck-lock      \ xb-chuck-lock
6  el2808-1 2constant xb-chuck-release   \ xb-chuck-release
7  el2808-1 2constant xb-chuck-blow      \ xb-chuck-blow
8  el2808-1 2constant xb-chuck-check     \ xb-chuck-check

\ EL2808-2

\ ----- Analog inputs -----

7 107 config-slave el3058

1  el3058 2constant m1-press-ain     \ m1壓力
2  el3058 2constant m1-flow-ain      \ m1流量

\ ----- Analog outputs -----

6 106 config-slave el4002

\ EL4002
1 el4002 2constant aqua-prcs-pump-aout  \ Sollwert Prozesspumpe (set value process pump)


\ ----- UART -----

13 113 config-slave EL6022              \ EL6022
1 EL6022 2constant PECM-RMT-ch-slv       \ Remoter channel and slave number
2 EL6022 2constant PECM-DSP-ch-slv       \ DSP communication channel and slave number



\ ----- I/O Actions -----

\ Dout 輸出指令
: +dout ( channel slave -- ) 1 -rot ec-dout! ;
: -dout ( channel slave -- ) 0 -rot ec-dout! ;

\ 加工機平台安全門
: platform-door@ ( -- flag ) platform-door-din ec-din@ ;
: .platform-door  ." platform_door|"  platform-door@  0 .r cr ;
\ Button
: sys-off-btn@ ( -- flag ) sys-off-din ec-din@ ;
: pw-btn@  ( -- flag ) pw-btn-din ec-din@ ;
: is-EMS?  ( -- flag ) ems-din ec-din@ not ;        \ 緊急停止訊號為反向


\ Aqua
: aqua-ems@ ( -- flag ) aqua-ems-din ec-din@ ;
: aqua-ready@ ( -- flag ) aqua-ready-din ec-din@ ;
: aqua-pump-running@ ( -- flag ) aqua-pump-running-din ec-din@ ;
: +aqua-rls-elect ( -- ) rls-elect-dout +dout ;
: -aqua-rls-elect ( -- ) rls-elect-dout -dout ;
: +aqua-prcs-valve ( -- ) prcs-valve-dout +dout ;
: -aqua-prcs-valve ( -- ) prcs-valve-dout -dout ;
: +aqua-prcs-pump-start ( -- ) prcs-pump-run-dout +dout ;
: -aqua-prcs-pump-start ( -- ) prcs-pump-run-dout -dout ;


\ system off output
: +sys-off ( -- ) sys-off-dout +dout ;

\ Power supply
: +pw-supply-on  ( -- ) pw-supply-dout +dout ;
: -pw-supply-on  ( -- ) pw-supply-dout -dout ;

\ 蜂鳴器
: +buzzer ( -- ) buzzer-dout +dout ;
: -buzzer ( -- ) buzzer-dout -dout ;

\ LED
: +pw-led ( -- ) pw-led-dout +dout ;
: -pw-led ( -- ) pw-led-dout -dout ;
: +cnc-led ( -- ) cnc-led-dout +dout ;
: -cnc-led ( -- ) cnc-led-dout -dout ;

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
\ chiller 開關 
: +chiller
;
: -chiller
;

\ 回傳是否有由量測電路來的短路訊號
: short-signal? ( -- flag )
  touch-din ec-din@ not      \ 訊號為反向
;
\ 回傳是否有由加工中短路保護電路來的短路訊號
: cutting-short? ( -- flag )
  short-alarm-ch-slv ec-din@ not        \ 訊號為反向
;

: cutting-arc? ( -- flag )
  false                                 \ 目前尚未接
;
\ 安全門上蓋
: upper-close?
  3 group! 
  1 pcs-p@ -3e mm f>
  2 pcs-p@ -3e mm f> and
;
: .upper-door  ." upper_door|"  upper-close?  0 .r cr ;

\ system ready?
: system-ready? ( -- flag )
  system-ready @
;

marker -work