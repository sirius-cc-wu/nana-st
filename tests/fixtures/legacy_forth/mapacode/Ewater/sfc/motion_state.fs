-work

\ 漏液時蜂鳴器常駐聲
variable water-err-buzzer  continuous-beep water-err-buzzer !

\ buffer 漏液檢知
: buffer-err?
  buffer-leaking-din ec-din@ not
;

\ 冰水機三通閥的漏水檢知
: IW-err?
  IW-leaking-din ec-din@ not
;

\ 中央台下方的漏水檢知
: mid-under-err?
  mid-under-leaking-din ec-din@ not
;

\ 過濾逆洗裝置漏水承接盤的漏水檢知
: backwash-err?
  backwash-leaking-din ec-din@ not
;
\ group1 過負載
: group1-overload?
  group1-overload-din ec-din@
;

\ group2 過負載
: group2-overload?
  group2-overload-din ec-din@
;

\ 加藥機異常
\ 
variable ignore-dosing-err-mode \ 忽略加藥機異常
: +ignore-dosing-err-mode
    ignore-dosing-err-mode on
;
: -ignore-dosing-err-mode
    ignore-dosing-err-mode off
;
: dosing-err?
  ignore-dosing-err-mode @ if
    false
  else
    NaN03-err-din ec-din@
    FeS-err-din ec-din@ or
    NaOH-err-din ec-din@ or
    HNO3-err-din ec-din@ or
  then
;
: +NaN03-b
  30000 NaN03-pump-aout ec-aout!
;
: -NaN03-b
  0 NaN03-pump-aout ec-aout!
;

: +FeSO4-b
  30000 FeSO4-pump-aout ec-aout!
;
: -FeSO4-b
  0 FeSO4-pump-aout ec-aout!
;

: +NaOH-b
  30000 NaOH-pump-aout ec-aout!
;
: -NaOH-b
  0 NaOH-pump-aout ec-aout!
;

: +HNO3-b
  30000 HNO3-pump-aout ec-aout!
;
: -HNO3-b
  0 HNO3-pump-aout ec-aout!
;

\ 髒水槽 0~32767 對應 0~1.9米
: dirty-water-lev@
  dirty-water-level-ain ec-ain@ s>f 0.00005798e f*
;

\ 反應槽水導度 0~32767 對應0~200mS/cm
: reactor-conduct@
  reactor-conductivit-ain ec-ain@ s>f 0.0061037e f*
;

\ 鹽水槽水導度 0~32767 對應0~200mS/cm
: NaCl-conduct@
  NaCl-conductivit-ain ec-ain@ s>f 0.0061037e f*
;

\ 反應槽ph值 0~32767 對應0~14
: reactor-ph@
  reactor-ph-ain ec-ain@ s>f 0.000427259e f*
;

\ 清水槽ph值 0~32767 對應0~14
: shimizu-ph@
  shimizu-ph-ain ec-ain@ s>f 0.000427259e f*
;

\ 0~32767 對應 0-50度
: shimizu-tmp@ ( F: -- tmp )
    shimizu-tmp-b ec-ain@ s>f 0.0015259e f*
;

\ 0~32767 對應 0-60mg/L
: cr6-conductivit@ ( F: -- cr6 )
    cr6-conductivit-ain ec-ain@ s>f 0.0018311e f*
;

\ 溫度上下限設置
fvariable tmp-high-limit 26e tmp-high-limit f!
fvariable tmp-low-limit 24e tmp-low-limit f!


\ 髒水槽分為4個準位 分別為
0 constant dirty-high
1 constant dirty-ready
2 constant dirty-warn
3 constant dirty-low
variable dirty-lev

variable m2-ready? m2-ready? on \ 用於水系統
variable m4-ready? m4-ready? on \ 用於水系統
: dirty-water-process
  dirty-water-lev@ fdup 1.6e f> if
    \ 過高時 停止加工
      fdrop 
      dirty-high dirty-lev !
    else 
      fdup 0.3e f< if
        \ 過低停止過濾
        fdrop
        dirty-low dirty-lev !
      else
        0.35e f< if
          \ do nothing
          dirty-warn dirty-lev !
        else
          dirty-ready dirty-lev !
        then
      then
  then
;
\ 清水槽分為3個準位 分別為
0 constant shimizu-ready
1 constant shimizu-warn
2 constant shimizu-toolow
variable shimizu-lev

: shimizu-water-process
  shimizu-low@ if             \ high時表示水位比較高
    shimizu-ready shimizu-lev !
    else 
      shimizu-toolow@ if
        shimizu-warn shimizu-lev !
      else
        shimizu-toolow shimizu-lev !
      then
  then
;

\ 無法加工的錯誤訊息
\ 為防止一直報 一次錯誤只報一個
variable showErrrMsg
: .ew-err-msg
  \ 髒水槽太高
  dirty-high dirty-lev @ = showErrrMsg @ and if
    ." error| Dirty too high ;A1001"
  then

  \ 清水槽太低
  shimizu-toolow shimizu-lev @ = showErrrMsg @ and if
    ." error| Shimizu too low ;A1002"
  then
  
  \ 加藥機異常
  dosing-err? showErrrMsg @ and if
    ." error| Dosing Error ;A1003"
  then
  
  \ Buffer Tank 漏液
  buffer-err? showErrrMsg @ and if
    ." error| Buffer Tank Error ;A1004"
  then

  \ group1 過負載
  group1-overload? showErrrMsg @ and if
    ." error| group1 overload ;A1005"
  then

  \ group2 過負載
  group2-overload? showErrrMsg @ and if
    ." error| group2 overload ;A1006"
  then

  \ 冰水機漏液
  IW-err? showErrrMsg @ and if
    ." error| Ice water error ;A1007"
  then

  \ 逆洗漏液
  backwash-err? showErrrMsg @ and if
    ." error| Backwash error ;A1008"
  then

  \ 中央台下方漏液
  mid-under-err? showErrrMsg @ and if
    ." error| Basement error ;A1009"
  then
  showErrrMsg off \ 有異常時只顯示一次
;

: ew-err-buzzer
    buffer-err?
    backwash-err? or
    mid-under-err? or if
        water-err-buzzer set-buzzer
    else
        water-err-buzzer unset-buzzer
    then
;

: aqua-state-process
  \ ew-ready
  shimizu-toolow shimizu-lev @ =      \ 清水槽太低
  dirty-high dirty-lev @ = or         \ 或髒水槽太高
  dosing-err? or                      \ 加藥機異常
  buffer-err? or                      \ buffer tank 漏液
  group1-overload? or                 \ group1 過負載
  group2-overload? or                 \ group2 過負載
  IW-err? or                          \ 冰水機漏液
  backwash-err? or                    \ 逆洗漏液
  mid-under-err? or                   \ 中央台下方漏液
  if                                  \ 停止加工
    ew-ready-dout -dout
    .ew-err-msg
  then

  shimizu-ready shimizu-lev @ =       \ 清水槽ready,表示水位夠高
  dirty-high dirty-lev @ = not and    \ 且髒水槽沒有過高
  dosing-err? not and                 \ 且加藥機沒有異常
  buffer-err? not and                 \ buffer tank 無漏液
  group1-overload? not and            \ group1 過負載
  group2-overload? not and            \ group2 過負載
  IW-err? not and                     \ 冰水機漏液
  backwash-err? not and               \ 逆洗漏液
  mid-under-err? not and              \ 中央台下方漏液
  if                                  \ 開放加工
    ew-ready-dout +dout
    showErrrMsg on                    \ 無異常時開啟此旗標,讓當有異常時可以顯示
  then

  \ 蜂鳴器
  ew-err-buzzer

  \ m2-ready
  dirty-ready dirty-lev @ = if
    m2-ready? on
  then

  dirty-low dirty-lev @ = if
    m2-ready? off
  then

  \ m4-ready
  shimizu-ready shimizu-lev @ = if
    m4-ready? on
  then

  shimizu-toolow shimizu-lev @ = if
    m4-ready? off
  then
;

: .ew-st
    \ 印出緊急停止狀態
    is-IPC-EMS? is-EW-EMS? or
    if
        ." EMS|true" cr
    else
        ." EMS|false" cr
    then
    
    \ 印出清水槽深度狀態
    ." shimizu_lev|" shimizu-lev @ 0 .r cr

    \ 印出髒水槽狀態
    ." dirty_lev|" dirty-lev @ 0 .r cr
    ." dirty_water_lev|" dirty-water-lev@ 4 1 f.r cr

    \ 印出反應槽酸鹼度
    ." reactor_conduct|" reactor-conduct@ 3 1 f.r cr
    \ 印出鹽水槽酸鹼度
    ." NaCl_conduct|"  NaCl-conduct@ 3 1 f.r cr
    \ 印出反應槽ph
    ." reactor_ph|" reactor-ph@ 3 2 f.r cr
    \ 印出清水槽ph
    ." shimizu_ph|" shimizu-ph@ 3 2 f.r cr
    \ 印出清水槽溫度
    ." shimizu_tmp|" shimizu-tmp@ 3 1 f.r cr
    \ 印出cr6濃度
    ." cr6_conductivit|" cr6-conductivit@ 3 1 f.r cr
    \ 印出加藥機有無異常
    dosing-err?
    if
        ." dosing_err|true" cr
    else
        ." dosing_err|false" cr
    then
;

marker -work