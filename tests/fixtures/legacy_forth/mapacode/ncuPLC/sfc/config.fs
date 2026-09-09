-work

\ ===== SFC version =====
: sfc-ver
  ." SFC-version|1.0.1" cr
;

\ ===== SFC configuration commands =====
\ 這裡增加的命令是為了避免專案中的 SFC 造成 botnana 測試時沒接 slave , motion server 就跑不起來的困擾。

variable ECM-configured
ECM-configured on

\ 若 alias 不存在就採用給定的 slave number
: config-slave ( slave alias -- ) ( input duff: name )
  dup ec-alias?
  if
    swap drop ec-a>n
  else
    drop ECM-configured off
  then
  create , does> @
;

\ 用於 SFC 載入完成後，要啟動最初的 step，若 ECM slaves config 不成功，就不啟動。
: start-sfc ( xt -- )
  ECM-configured @
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
variable ECM-power-state        \ power on/off 狀態
variable ECM-motion-state       \ < 0(idle), 1(jogging), 2(machining), 3(meas) >
variable ECM-going              \ GoECM 工作/休眠旗標
variable GoECM-in-idle          \ Go ECM in idle 旗標
variable GoECM-fault            \ 放電加工異警旗標
variable GoECM-V-fault          \ 振動軸圖形變形
variable chiller-state          \ chiller開關狀態
fvariable working-rate-limit    \ 加工時間的執行率下限
\ groups
1 constant meas-group



\ ===== EtherCAT Slave configurations =====
\ 這裡為 ECM 的 Slave, channel 定義和全域變數的配置檔

\ ----- Drives -----

1 101 config-slave slave-x              \ X slave number
1 slave-x 2constant drive-x             \ X channel and slave number
1 constant ECM-X-axis                   \ X axis number

2 102 config-slave slave-y              \ Y slave number
1 slave-y 2constant drive-y             \ Y channel and slave number
2 constant ECM-Y-axis                   \ Y axis number

3 103 config-slave slave-z              \ Z slave number
1 slave-z 2constant drive-z             \ Z channel and slave number
3 constant ECM-Z-axis                   \ Z axis number

4 constant slave-v                      \ V slave number，因 V 軸設定 alias 有問題，所以直接指定 slave position
1 slave-v 2constant drive-v             \ V channel and slave number
4 constant ECM-V-axis                   \ V axis number



\ ----- Digital inputs -----

6 202 config-slave el1819
7 203 config-slave el1809

\ EL1819
1  el1819 2constant ems-din           \ Emergency Button
2  el1819 2constant sys-off-din       \ System off Button
3  el1819 2constant 3phase-din        \ 三相檢知器
4  el1819 2constant pw-btn-din        \ power on/off Button
5  el1819 2constant ECM-pause-ch-slv  \ pause channel and slave number
6  el1819 2constant ECM-reset-ch-slv  \ reset channel and slave number
9  el1819 2constant ECM-start-ch-slv  \ start channel and slave number
11 el1819 2constant arc-short         \ 電弧
12 el1819 2constant aqua-ems-din      \ EMS of Aqua
13 el1819 2constant aqua-ready-din    \ Aqua Ready
14 el1819 2constant aqua-pump-running-din \ Puckmeldung Pumpe (Pump running)
15 el1819 2constant ECM-done-ch-slv   \ ECM_done channel and slave number
16 el1819 2constant short-alarm-ch-slv  \ short-alarm channel and slave number

\ EL1809
1  el1809 2constant platform-door-din \ 加工平台安全門
4  el1809 2constant aqua-bt-upper-din \ Ball-1
5  el1809 2constant aqua-bt-lower-din \ Ball-2
9  el1809 2constant power-door        \ pem門的開關
16 el1809 2constant short-ch-slv      \ short channel and slave number


\ ----- Digital outputs -----

8 204 config-slave el2809-1
9 205 config-slave el2809-2

\ EL2809-1
1  el2809-1 2constant sys-off-dout      \ System off
2  el2809-1 2constant buzzer-dout       \ 蜂鳴器
3  el2809-1 2constant g-led-dout        \ Ready(綠燈)
4  el2809-1 2constant y-led-dout        \ Warning(黃燈)
5  el2809-1 2constant r-led-dout        \ Error(紅燈)
6  el2809-1 2constant aqua-prcs-pump-start-dout  \ Process pump run (Pumpe star)
7  el2809-1 2constant aqua-prcs-valve-dout  \ Process valve (Prozessventil)
8  el2809-1 2constant aqua-rls-elect-dout \ Release electrolyte (Freigabe Elektrolyt)
11 el2809-1 2constant pw-led-dout       \ power on/off LED
12 el2809-1 2constant pause-LED-ch-slv  \ pause LED channel and slave number
13 el2809-1 2constant start-LED-ch-slv  \ start LED channel and slave number
14 el2809-1 2constant cnc-led-dout      \ CNC 備妥燈
15 el2809-1 2constant pw-supply-dout    \ 驅動器電源供應
16 el2809-1 2constant pem-dout          \ PEM 電源
\ EL2809-2
2  el2809-2 2constant air-chuck         \ 空氣夾頭
7  el2809-2 2constant aqua-bt-pump-dout \ Buffer tank pump
9  el2809-2 2constant vi-trigger        \ vi-trigger 用於繪圖時對準準位
10 el2809-2 2constant Go-ECM-ch-slv     \ Go_ECM channel and slave number
11 el2809-2 2constant AC20V-control-ch-slv  \ AC20V control channel and slave number
12 el2809-2 2constant chiller           \ 冰水機到pem的開關, 用來冷卻pem


\ ----- Analog outputs -----

13 211 config-slave el4002

\ EL4002
1 el4002 2constant aqua-prcs-pump-aout  \ Sollwert Prozesspumpe (set value process pump)
2 el4002 2constant v-air-pump-aout

\ ----- Analog inputs -----

12 208 config-slave el3058-1
14 209 config-slave el3058-2
15 210 config-slave el3058-3

\ EL3058-1
1 el3058-1 2constant v-flow-ain    \ V 軸流量計
2 el3058-1 2constant v-press-ain   \ V 軸壓力感測器
6 el3058-1 2constant bft-h2-sensor      \ Buffer tank 氫爆感測器
8 el3058-1 2constant platform-h2-sensor \ 加工平台氫爆感測器

\ EL3058-2
1 el3058-2 2constant aqua-fb-p-ain    \ Process electrolyte pressure (Feedback/Prozessdrunck)
2 el3058-2 2constant aqua-flow        \ aqua的流量
3 el3058-2 2constant aqua-temp        \ aqua的溫度

\ EL3058-3

\ EL3062



\ ----- UART -----

17 212 config-slave EL6022-1              \ EL6022
18 215 config-slave EL6022-2              \ EL6022
\ EL6022-1 
1 EL6022-1 2constant ECM-RMT-ch-slv       \ Remoter channel and slave number
2 EL6022-1 2constant ECM-DSP-ch-slv       \ DSP communication channel and slave number

\ EL6022-2
1 EL6022-2 2constant modbus-ch-slv       \ modbus channel and slave number


\ ----- I/O Actions -----

\ Dout 輸出指令
: +dout ( channel slave -- ) 1 -rot ec-dout! ;
: -dout ( channel slave -- ) 0 -rot ec-dout! ;

\ Button
: sys-off-btn@ ( -- flag ) sys-off-din ec-din@ ;
: pw-btn@  ( -- flag ) pw-btn-din ec-din@ ;
: is-EMS?  ( -- flag ) ems-din ec-din@ not ;        \ 緊急停止訊號為反向

\ Aqua
: aqua-ems@ ( -- flag ) aqua-ems-din ec-din@ ;
: aqua-ready@ ( -- flag ) aqua-ready-din ec-din@ ;
: aqua-pump-running@ ( -- flag ) aqua-pump-running-din ec-din@ ;
: +aqua-rls-elect ( -- ) aqua-rls-elect-dout +dout ;
: -aqua-rls-elect ( -- ) aqua-rls-elect-dout -dout ;
: +aqua-prcs-valve ( -- ) aqua-prcs-valve-dout +dout ;
: -aqua-prcs-valve ( -- ) aqua-prcs-valve-dout -dout ;
: +aqua-prcs-pump-start ( -- ) aqua-prcs-pump-start-dout +dout ;
: -aqua-prcs-pump-start ( -- ) aqua-prcs-pump-start-dout -dout ;


\ Buftank
: aqua-bt-upper@ ( -- flag ) aqua-bt-upper-din ec-din@ ;
: aqua-bt-lower@ ( -- flag ) aqua-bt-lower-din ec-din@ ;
: +aqua-bt-pump ( -- ) aqua-bt-pump-dout +dout ;
: -aqua-bt-pump ( -- ) aqua-bt-pump-dout -dout ;

\ Feedback
: aqua-fb-p@ ( F: -- bar )
  aqua-fb-p-ain ec-ain@ s>f
  \ 將讀到的bit值轉為mA(因為EL3058是將接收到的電流值轉為bit值)
  0.000488296e f* 4e f+
  \ TODO: 將電流值(mA)在轉為氣壓值(bar)
  \ 或許可以直接從bit值轉為氣壓值?!
;

\ V 軸感測器
: v-press@ ( F: -- bar )  v-press-ain ec-ain@ s>f 0.00061037e ( 20 [L/min] / 32767 [bit] ) f*  ;
: v-flow@  ( F: -- L/min ) v-flow-ain ec-ain@ s>f 0.00030131e ( 10 [Bar] / 32767 [bit] ) f* ;
: .v-press ." v-press|" v-press@ f. cr ;
: .v-flow  ." v-flow|"  v-flow@  f. cr ;

\ 氫爆感測器
: platform-h2@  ( F: -- percentage % ) platform-h2-sensor ec-ain@ s>f 0.003052e ( 100[%] / 32767 [bit] ) f* ;
: bft-h2@       ( F: -- percentage % ) bft-h2-sensor ec-ain@ s>f 0.003052e ( 100[%] / 32767 [bit] ) f* ;


\ 加工機平台安全門
: platform-door@ ( -- flag ) platform-door-din ec-din@ not ;
: .platform-door  ." platform_door|"  platform-door@  0 .r cr ;

\ 三相檢知
: 3phase@ ( -- flag ) 3phase-din ec-din@ ;

\ PEM
: +pem ( -- ) pem-dout +dout ;
: -pem ( -- ) pem-dout -dout ;

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

\ air-pump-aout
variable air-pump
\ 關閉air-pump
: close-air-pump
  0 air-pump !
  0 v-air-pump-aout ec-aout!
;

\ 確保輸入在0~32767之間
: ++air-pump ( value --)
  air-pump @ + 32767 min 0 max dup
  air-pump ! v-air-pump-aout ec-aout!
;

\ chiller 開關 
: +chiller
  chiller +dout                           \ 開啟 chiller
  chiller-state on
;
: -chiller
  chiller -dout                           \ 關閉 chiller
  chiller-state off
;

: .chiller-state
    chiller-state @
    if
        ." chiller_state|true" cr
    else
        ." chiller_state|false" cr
    then
;

\ 回傳是否有由量測電路來的短路訊號
: short-signal? ( -- flag )
  short-ch-slv ec-din@ not      \ 訊號為反向
;

\ 回傳是否有由加工中短路保護電路來的短路訊號
: cutting-short? ( -- flag )
  short-alarm-ch-slv ec-din@ not        \ 訊號為反向
;

: cutting-arc? ( -- flag )
  arc-short ec-din@ not                 \ 訊號為反向
;

\ system ready?
: system-ready? ( -- flag )
  system-ready @
;

marker -work