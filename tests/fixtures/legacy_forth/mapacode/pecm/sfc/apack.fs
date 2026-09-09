-work

\ index 0 為目前使用的條件
\ 設定 index 0 條件的應該還要伴隨實際的運動或是硬體設定命令

100 constant apks-len
create apks-step apks-len cells allot
create apks-ecode apks-len cells allot
create apks-ae falign apks-len floats allot
create apks-l falign apks-len floats allot
create apks-rg falign apks-len floats allot
create apks-zg falign apks-len floats allot
create apks-q apks-len cells allot
create apks-md apks-len cells allot
create apks-vl falign apks-len floats allot
create apks-ip apks-len cells allot
create apks-on apks-len cells allot
create apks-of apks-len cells allot
create apks-al apks-len cells allot
create apks-spd falign apks-len floats allot
create apks-co falign apks-len floats allot
create apks-ph falign apks-len floats allot
create apks-tp falign apks-len floats allot
create apks-fp falign apks-len floats allot
create apks-bp falign apks-len floats allot
create apks-vf falign apks-len floats allot
create apks-va falign apks-len floats allot
create apks-vt falign apks-len floats allot


\ apack step
: apks-step! ( step index  -- ) 
    apks-step param! ; 
: apks-step@ ( index  -- step )
    apks-step param@ ; 
: apk-step! ( step -- ) 
    0 apks-step param! ; 
: apk-step@ ( -- step )
    0 apks-step param@ ; 

fvariable cur-trigger

\ apack ecode
: apks-ecode! ( ecode index  -- ) 
    apks-ecode param! ; 
: apks-ecode@ ( index  -- ecode )
    apks-ecode param@ ; 
: apk-ecode! ( ecode -- ) 
    0 apks-ecode param! ; 
: apk-ecode@ ( -- ecode )
    0 apks-ecode param@ ; 
   
\ apack ae
: apks-ae! ( index  -- ) ( F: ae -- ) 
    apks-ae fparam! ; 
: apks-ae@ ( index  -- ) ( F: -- ae )
    apks-ae fparam@ ; 
: apk-ae! ( F: ae -- ) 
    0 apks-ae fparam! ; 
: apk-ae@ ( F: -- ae )
    0 apks-ae fparam@ ; 
   
\ apack l
: apks-l! ( index  -- ) ( F: l -- ) 
    apks-l fparam! ; 
: apks-l@ ( index  -- ) ( F: -- l )
    apks-l fparam@ ; 
: apk-l! ( F: l -- ) 
    0 apks-l fparam! ; 
: apk-l@ ( F: -- l ) 
    0 apks-l fparam@ ; 
   
\ apack rg
: apks-rg! ( index  -- ) ( F: rg -- ) 
    apks-rg fparam! ; 
: apks-rg@ ( index  -- ) ( F: -- rg )
    apks-rg fparam@ ; 
: apk-rg! ( F: rg -- )
    0 apks-rg fparam! ; 
: apk-rg@ ( F: -- rg ) 
    0 apks-rg fparam@ ;

\ apack zg
: apks-zg! ( index  -- ) ( F: zg -- )
    apks-zg fparam! ; 
: apks-zg@ ( index  -- ) ( F: -- zg )
    apks-zg fparam@ ; 
: apk-zg! ( F: zg -- ) 
    0 apks-zg fparam! ; 
: apk-zg@ ( F: -- zg )
    0 apks-rg fparam@ ;
   
\ apack q
: apks-q! ( q index  -- ) 
    apks-q param! ; 
: apks-q@ ( index  -- q )
    apks-q param@ ; 
: apk-q! ( q -- )
    0 apks-q param! ; 
: apk-q@ ( -- q ) 
    0 apks-q param@ ; 
   
\ apack md
: apks-md! ( md index  -- ) 
    apks-md param! ; 
: apks-md@ ( index  -- md )
    apks-md param@ ; 
: apk-md@ ( -- md ) 
    0 apks-md param@ ;                   

\ apack vl
: apks-vl! ( index  -- ) ( F: vl -- )
    apks-vl fparam! ; 
: apks-vl@ ( index  -- ) ( F: -- vl )
    apks-vl fparam@ ; 
: apk-vl! ( F: vl -- ) 
    munk-configured @ if
        fdup 1000e f* f>s set-v1!               \ 設定傳送給MUNK參數
        munk-cmd-write
    then
    0 apks-vl fparam! ; 
: apk-vl@ ( F: -- vl ) 
    0 apks-vl fparam@ ;
   
\ apack ip
: apks-ip! ( ip index  -- ) 
    apks-ip param! ; 
: apks-ip@ ( index  -- ip )
    apks-ip param@ ; 
: apk-ip! ( ip -- ) 
    munk-configured @ if
        dup set-a1!               \ 設定傳送給MUNK參數
        munk-cmd-write
    then
    0 apks-ip param! ; 
: apk-ip@ ( -- ip ) 
    0 apks-ip param@ ; 
   
\ apack on
: apks-on! ( on index  -- ) 
    apks-on param! ; 
: apks-on@ ( index  -- on )
    apks-on param@ ; 
: apk-on! ( on -- )
    dup ON!                         \ 設定傳送給dsp的封包參數
    0 apks-on param! ; 
: apk-on@ ( -- on ) 
    0 apks-on param@ ;  
   
\ apack of
: apks-of! ( of index  -- ) 
    apks-of param! ; 
: apks-of@ ( index  -- of )
    apks-of param@ ; 
: apk-of! ( of -- )
    dup OFF!                         \ 設定傳送給dsp的封包參數
    0 apks-of param! ; 
: apk-of@ ( -- of ) 
    0 apks-of param@ ; 
   

\ apack al
: apks-al! ( al index  -- )
    apks-al param! ; 
: apks-al@ ( index  -- al )
    apks-al param@ ; 
: apk-al! ( al -- ) 
    dup Short-detect-level!          \ 設定傳送給dsp的封包參數
    0 apks-al param! ; 
: apk-al@ ( -- al ) 
    0 apks-al param@ ; 

\ apack spd
: apks-spd! ( index  -- ) ( F: spd -- ) 
    apks-spd fparam! ; 
: apks-spd@ ( index  -- ) ( F: -- spd )
    apks-spd fparam@ ; 

: apk-spd@ ( F: -- spd ) 
    0 apks-spd fparam@ ;

: apk-spd! ( F: spd -- )
    \ feed-mode != 一般模式:1  or feed-ratio = 1 or apk-spd@ = 0 or new-spd = 0 則直接變速
    \ 若為false,則照比例變速,而feed-ratio最大為1
    feed-mode @ 1 <> 
    1e feed-ratio f@ 0.001e f~ or  
    apk-spd@ 0e 0.001e f~ or
    fdup 0e 0.001e f~ or                            ( flag ) ( F: spd   )
    if
        fdup
        1 group! feed-ratio f@ f* mm/min vcmd!      ( F: spd )
    else
        fdup                                        ( F: spd spd  )
        apk-spd@ feed-ratio f@ f* fswap f/          
        1e fmin                                     ( F: spd feed-ratio  )
        fdup feed-ratio f!
        1e f> if                                    ( flag ) ( F: spd )
            fdup
            1 group! mm/min vcmd!
        then                                        ( F: spd )
    then
    0 apks-spd fparam!                              ( -- )
;

\ apack co
: apks-co! ( index  -- ) ( F: co -- ) 
    apks-co fparam! ; 
: apks-co@ ( index  -- ) ( F: -- co )
    apks-co fparam@ ; 
: apk-co! ( F: co -- ) 
    0 apks-co fparam! ; 
: apk-co@ ( F: -- co ) 
    0 apks-co fparam@ ;
   
\ apack ph
: apks-ph! ( index  -- ) ( F: ph -- ) 
    apks-ph fparam! ; 
: apks-ph@ ( index  -- ) ( F: -- ph )
    apks-ph fparam@ ; 
: apk-ph! ( F: ph -- ) 
    0 apks-ph fparam! ; 
: apk-ph@ ( F: -- ph ) 
    0 apks-ph fparam@ ;
   
\ apack tp
: apks-tp! ( index  -- ) ( F: tp -- ) 
    apks-tp fparam! ; 
: apks-tp@ ( index  -- ) ( F: -- tp )
    apks-tp fparam@ ; 
: apk-tp! ( F: tp -- ) 
    0 apks-tp fparam! ; 
: apk-tp@ ( F: -- tp ) 
    0 apks-tp fparam@ ;      

\ apack fp
: apks-fp! ( index  -- ) ( F: fp -- ) 
    apks-fp fparam! ; 
: apks-fp@ ( index  -- ) ( F: -- fp )
    apks-fp fparam@ ; 
: apk-fp! ( F: fp -- ) 
    fdup cutting-aqua-value f!
    set-aqua-value
    0 apks-fp fparam! ; 
: apk-fp@ ( F: -- fp ) 
    0 apks-fp fparam@ ;       

\ apack bp
: apks-bp! ( index  -- ) ( F: bp -- ) 
    apks-bp fparam! ; 
: apks-bp@ ( index  -- ) ( F: -- bp )
    apks-bp fparam@ ; 
: apk-bp! ( F: bp -- ) 
    0 apks-bp fparam! ; 
: apk-bp@ ( F: -- bp ) 
    0 apks-bp fparam@ ;        

\ apack vf
: apks-vf! ( index  -- ) ( F: vf -- )
  apks-vf fparam! ; 
: apks-vf@ ( index  -- ) ( F: -- vf ) 
  apks-vf fparam@ ; 
: apk-vf@ ( F: -- vf ) 
    0 apks-vf fparam@ ;
: apk-vf! ( F: vf -- ) 
    0 apks-vf fparam! 
; 

\ apack va & vt
: apks-va! ( index  -- ) ( F: va -- ) 
    apks-va fparam! ; 
: apks-va@ ( index  -- ) ( F: -- va ) 
    apks-va fparam@ ; 
: apk-va@ ( F: -- va ) 
    0 apks-va fparam@ ;
: apk-va! ( F: va -- )
    0 apks-va fparam!
;
   
\ apack vt
: apks-vt! ( index  -- ) ( F: vt -- ) 
    apks-vt fparam! ; 
: apks-vt@ ( index  -- ) ( F: -- vt ) 
    apks-vt fparam@ ; 
: apk-vt@ ( F: -- vt ) 
    0 apks-vt fparam@ ;
: apk-vt! ( F: vt -- )
    0 apks-vt fparam! 
; 

: set-power-mode!
    apk-va@ 0e 0.0001e f~ 
    if
        apk-md@ 1 = 
        if 
            2                   \ 直流+沒振動, 因實體電路尚未規劃, 目前都設定為2, 未來將改回0
        else 
            2                   \ plus+沒振動, 因實體電路尚未規劃, 目前都設定為2, 未來將改回1
        then
    else
        2                       \ 直流/plus+振動
    then Power-mode!
;

: apk-md! ( md -- )
    0 apks-md param!
    set-power-mode! 
; 


\ 將選取的條件設定到 apk
: to-apk ( index  -- )
    dup apks-step@ apk-step!
    dup apks-ecode@ apk-ecode!
    dup apks-ae@ apk-ae!
    dup apks-l@ apk-l!
    dup apks-rg@ apk-rg! 
    dup apks-zg@ apk-zg! 
    dup apks-q@ apk-q! 
    dup apks-md@ apk-md! 
    dup apks-vl@ apk-vl! 
    dup apks-ip@ apk-ip! 
    dup apks-on@ apk-on! 
    dup apks-of@ apk-of! 
    dup apks-al@ apk-al! 
    dup apks-spd@ apk-spd! 
    dup apks-co@ apk-co! 
    dup apks-ph@ apk-ph! 
    dup apks-tp@ apk-tp! 
    dup apks-fp@ apk-fp!
    dup apks-bp@ apk-bp! 
    dup apks-vf@ apk-vf! 
    dup apks-va@ apk-va!
    dup apks-vt@ apk-vt! 
    drop
;

\ Output APACK information
: .apack ( index -- )
    ." apk_step." dup 0 .r  ." |" dup apks-step@ 0 .r
    ." |apk_ecode." dup 0 .r  ." |" dup apks-ecode@ 0 .r
    ." |apk_ae." dup 0 .r  ." |" dup apks-ae@ 4 2 f.r
    ." |apk_l." dup 0 .r  ." |" dup apks-l@ 4 2 f.r
    ." |apk_rg." dup 0 .r  ." |" dup apks-rg@ 4 2 f.r
    ." |apk_zg." dup 0 .r  ." |" dup apks-zg@ 4 2 f.r
    ." |apk_q." dup 0 .r  ." |" dup apks-q@ 0 .r
    ." |apk_md." dup 0 .r  ." |" dup apks-md@ 0 .r
    ." |apk_vl." dup 0 .r  ." |" dup apks-vl@ 4 2 f.r
    ." |apk_ip." dup 0 .r  ." |" dup apks-ip@ 0 .r
    ." |apk_on." dup 0 .r  ." |" dup apks-on@ 0 .r
    ." |apk_of." dup 0 .r  ." |" dup apks-of@ 0 .r
    ." |apk_al." dup 0 .r  ." |" dup apks-al@ 0 .r
    ." |apk_spd." dup 0 .r  ." |" dup apks-spd@ 4 2 f.r
    ." |apk_co." dup 0 .r  ." |" dup apks-co@ 4 2 f.r
    ." |apk_ph." dup 0 .r  ." |" dup apks-ph@ 4 2 f.r
    ." |apk_tp." dup 0 .r  ." |" dup apks-tp@ 4 2 f.r
    ." |apk_fp." dup 0 .r  ." |" dup apks-fp@ 4 2 f.r
    ." |apk_bp." dup 0 .r  ." |" dup apks-bp@ 4 2 f.r
    ." |apk_vf." dup 0 .r  ." |" dup apks-vf@ 4 2 f.r
    ." |apk_va." dup 0 .r  ." |" dup apks-va@ 3 3 f.r
    ." |apk_vt." dup 0 .r  ." |" dup apks-vt@ 4 2 f.r cr
    drop
;


variable end-step
variable current-step
variable past-step 999 past-step !

: decide-end-step ( -- )
    0                                         ( 1 )
    begin                                     ( n )
      dup 1+ apks-ecode@ 0<>                  ( n flag )
    while                                     ( n )
      1+                                      ( n+1 )
    repeat                                    ( n )
    end-step !                                ( )
;

: decide-current-step ( -- )  
    1 group!
    1 apks-l@ mm PECM-user-axis @ pcs-p@ f<            ( flag )
    if
      1 current-step !
    else
      1                                                 ( 1 ) 
      begin                                             ( n )
        dup end-step @ > not 
        over apks-l@ mm PECM-user-axis @ pcs-p@ f> and                ( n flag )
      while                                             ( n )
        dup current-step !                              ( n )
        1+                                              ( n+1 )
      repeat                                            ( n )
      drop                                              (  )
    then
;

: setting-parameter ( -- )
    decide-current-step

    current-step @ dup
    past-step @ <> 
    if
      dup past-step !
      dup to-apk
    then 
    drop
;
\ ===== SFC apack_communication =====
\
\     T0          T1        T2         T3
\ S0--+----->S1---+--->S2---+---> S3---+
\            ^                         |
\            |                         |
\            +-------------------------+

\ ---+----------+-------------------------------------------------------------------------------------
\ S0 | apack-S0 | 設定apack初始值
\ ---+----------+-------------------------------------------------------------------------------------
\ S1 | apack-S1 | 等待 g111? = true 旗標（g111啟動）
\ ---+----------+-------------------------------------------------------------------------------------
\ S2 | apack-S2 | 計算共有幾個step和設定apack條件
\ ---+----------+-------------------------------------------------------------------------------------
\ S3 | apack-S3 | 根據深度更新apack條件,直到加工結束 (g111? = false)
\ ---+----------+-------------------------------------------------------------------------------------
\
\ ---+----------+-------------------------------------------------------------------------------------
\ T0 | apack-T0 | apack-ready? = true 表示初始值已設定
\ ---+----------+-------------------------------------------------------------------------------------
\ T1 | apack-T1 | g111? 旗標 表示g111啟動
\ ---+----------+-------------------------------------------------------------------------------------
\ T2 | apack-T2 | true
\ ---+----------+-------------------------------------------------------------------------------------
\ T3 | apack-T3 | 由 g111? 旗標判斷 g111 是否加工完畢
\ ---+----------+-------------------------------------------------------------------------------------

variable apack-ready? false apack-ready? ! 
: apack-S0

    munk-configured @ if
        +munk-clear-err
        munk-init
        trigger-mode manual-op-mode!
        5 proc-time-h!
        munk-cmd-write 
    then

    0 apk-step!         \   1~20
    0 apk-ecode!        \   三位數
    1e apk-ae!          \   1~100000 mm2
    999e apk-l!         \   -999.9999 ~ 999.9999 mm    
    0e apk-rg!          \   0.000~0.999 mm
    0e apk-zg!          \   0.000~0.999 mm
    1211 apk-q!         \   0000 ~ 2199 
    1 apk-md!           \   1直流/2方波
    15e apk-vl!          \   5 ~ 20 V
    600 apk-ip!          \   10~20000A
    1000 apk-on!        \   10 ~ 9999 uS
    5000 apk-of!        \   10 ~ 9999 uS
    1 apk-al!           \   0-31
    10e apk-spd!        \   0.01 ~ 10.0 mm/min
    1e apk-co!          \   1 ~ 1000 uS
    7e apk-ph!          \   4 ~ 10 pH
    20e apk-tp!         \   攝氏 20 ~ 40 度
    1e apk-fp!          \   1 ~ 15 Bar
    1e apk-bp!          \   1 ~ 15 Bar
    0.1e apk-vf!        \   0.5 ~ 50.0 Hz
    0e apk-va!          \   0.0 ~ 0.5 mm   
    20e apk-vt!          \   1% ~ 100%

    true apack-ready? !
;
: apack-S1 ( waiting ) ;
: apack-S2 
    0 past-step !
    decide-end-step
    setting-parameter
;
: apack-S3 
    setting-parameter 
;

: apack-T0 apack-ready? @ ; 
: apack-T1 g111? machining-mode @ nc = and ; 
: apack-T2 true ;
: apack-T3 apack-T1 not ;

  step apack-S0
  step apack-S1
  step apack-S2
  step apack-S3

  transition apack-T0
  transition apack-T1
  transition apack-T2
  transition apack-T3

' apack-S0 ' apack-T0 -->
' apack-T0 ' apack-S1 -->
' apack-S1 ' apack-T1 -->
' apack-T1 ' apack-S2 -->
' apack-S2 ' apack-T2 -->
' apack-T2 ' apack-S3 -->
' apack-S3 ' apack-T3 -->
' apack-T3 ' apack-S1 -->


marker -work
