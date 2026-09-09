-work

\ 碰邊無視
: +touch-ignore ( -- )
    touch-ignore on
    ." Touch ignore." cr
;

\ 碰邊有視
: -touch-ignore ( -- )
    touch-ignore off
    ." Not touch ignore." cr
;

\ 量測重覆次數
variable detect-times 3 detect-times !
\ 量測退後距離
fvariable reverse-pitch -0.5e mm reverse-pitch f!
\ 量測偵測距離
fvariable detect-pitch 0.2e mm detect-pitch f!
\ 量測基準點
fvariable base-pos
\ 量測後退速度
fvariable back-rate 100.0e mm/min back-rate f!
\ 第一次量測速度
fvariable detect-v1 30.0e mm/min detect-v1 f!
\ 第二次量測速度
fvariable detect-v2 3.0e mm/min detect-v2 f!

\ 尋孔中心的量測速度
fvariable center-rate 10.0e mm/min center-rate f!
\ 預估孔中心直徑(孔最大距離)
fvariable diameter
\ 孔中心基準點,目前只存xy
fvariable centerX
fvariable centerY
fvariable centerX+
fvariable centerX-
fvariable centerY+
fvariable centerY-
variable search-center-done?
variable search-ending-buzzer single-solid-beep search-ending-buzzer !

\ 柱心
fvariable column-rate 10.0e mm/min column-rate f!
variable search-column-done?
\ index 0 不使用
create col-distance falign 5 floats allot
\ col-axis 因為xy軸移動共數次 由此定義移動哪一軸
create col-axis falign 5 cells allot
\ col-dir 因為xy軸移動數次 由此定義移動哪一個方向
create col-dir falign 5 cells allot
create col-touch-p falign 5 floats allot
create col-temp-p falign 3 floats allot
create col-center-p falign 3 floats allot

\ 量測目標位置
\ index 0 不使用
create distances falign 0e f, 0e f, 0e f, 0e f,
\ 量測結果
create detect-records falign 0e f, 0e f, 0e f, 0e f, 0e f,

\ 是否可進行量測旗標
variable measure-allowed
\ 不可執行量測
: -meas-allowed ( -- )
    FALSE measure-allowed !
;

\ 可執行量測
: +meas-allowed ( -- )
    TRUE measure-allowed !
;

\ 回傳是否可以執行量測
: meas-allowed? ( -- flag )
    PECM-power-state @                       \ power on
    PECM-motion-state @ idle = and           \ 且 motion idle
    feedhold? not and                       \ 且 not in feedhold
    is-EMS? not and                         \ 且緊急停止未觸發
    PECM-on-sl? not and                      \ 且未碰觸軟體極限
    is-short? not and                       \ 且無量測短路
;

\ 印出無法進行量測之異警
: .meas-errors ( -- )
    PECM-power-state @ not
    if
        ." error|Not power on. Start measuring failed.;A1301" cr
    then
    PECM-motion-state @ idle <>
    if
        ." error|ECM not idle. Start measuring failed.;A1302" cr
    then
    feedhold?
    if
        ." error|ECM in feedhold. Start measuring failed.;A1303" cr
    then
    is-EMS?
    if
        ." error|In EMS. Start measuring failed.;A1304" cr
    then
    PECM-on-sl?
    if
        ." error|On software limit. Start measuring failed.;A1305" cr
    then
    is-short?
    if
        measure-on?
        if
            ." error|In short. Start measuring failed.;A1306" cr
        else
            ." error|Short protection curcuit not enabled. Start measuring failed.;A1307" cr
        then
    then
;
: .search-errors-msg
    is-EMS?
    if
        ." error|In EMS. Search failed.;A1309" cr
    then
    PECM-on-sl?
    if
        ." error|On software limit. Search failed.;A1310" cr
    then
    ." error|No search edge found. ;A1311" cr
;
\ 設定目標位置
: target-pos! ( axis -- ) ( F: distance -- )
    4 1 do
        i pcs-p@ i distances fparam!
    loop
    dup pcs-p@ f+ distances fparam!
;

\ 取得目標位置
: target-pos@ ( F: -- dx dy dz )
    4 1 do
        i floats distances faligned + f@
    loop
;

\ reset detect result
: 0detect-records ( -- )
    5 0 do
        0e i floats detect-records faligned + f!
    loop
;

\ 印出 detect result
\ 依照 detect-times 來印出量測的結果。
: .detect-records ( -- )
    ." detect-records|"
    detect-times @ 0 do
        i floats detect-records faligned + f@ 0 7 f.r
        i detect-times @ 1 - <> if
            ." ,"
        then
    loop
;

\ 移動指定距離
: move-by-distance ( axis -- ) ( F: distance feed-rate -- )
    meas-group group! gstart \ 啟動量測軸組
    0path 1 pcs-p@ 2 pcs-p@ 3 pcs-p@ move3d
    feedrate@ fswap feedrate!    ( F: distance feedrate-old )
    fswap target-pos! target-pos@ line3d
    feedrate!
    \ 確認是否已停止
    pause pause \ 因為要等待軸組啟動
    begin
        meas-group group! gstop? not
    while
        pause
    repeat
;

\ 印出未偵測到量測邊界
: .edge-meas-search-error ( -- )
    .search-errors-msg
;

\ 確認是否未到量測邊界
: search-error? ( -- flag )
    \ 若有偵測到邊界，就不會走到終點。
    meas-group group! gend?
;

\ 計算量測值總合
fvariable edge-sum
\ 最大量測值
fvariable edge-max
\ 最小量測值
fvariable edge-min
\ 碰邊量測平均值
fvariable edge-mean
\ 碰邊量測差異值
fvariable edge-dev

\ 量測結果
\ detect-postion 是依照 detect-times 設定來決定浮點堆疊上有多少個浮點數。
: edge-result ( F: detect-postion... -- )
    0e edge-sum f!
    detect-times @ 0 do
        fdup edge-sum f@ f+ edge-sum f!
        \ 找出 max 與 min
        i 0= if
            fdup edge-max f! edge-min f!
        else
            fdup edge-max f@ fmax edge-max f!
            edge-min f@ fmin edge-min f!
        then
    loop

    \ 計算量測平均值
    edge-sum f@ detect-times @ s>f f/ edge-mean f!
    \ 計算差異值
    edge-max f@ edge-mean f@ f-
    edge-mean f@ edge-min f@ f- fmax edge-dev f!
    ." |edge-mean|" edge-mean f@ 0 7 f.r
    ." |edge-max|" edge-max f@ 0 7 f.r
    ." |edge-min|" edge-min f@ 0 7 f.r
    ." |edge-dev|" edge-dev f@ 0 7 f.r cr
;

\ 碰邊量測結束
: edge-meas-end ( -- )
    ending motion-status max!
    meas-group group! -group
;

\ 碰邊量測
\ 正常流程:
\   使用 MCS 座標系，開啟短路有視，使用第一段速移動到量測基準位罝。
\   由量測次數重覆量測。
\   重覆量測動作：到達量測基本位罝後, 使用後退速沿指定量測方向退指定距離。
\                 再由第一段速前進指定量測距離，並紀錄量測到的位置。
\   最後將紀錄量測到的位置，計算量測位置的平均值與差異值。
\
\ 異常流程:
\ 沿循邊方向運動 distance 距離後沒有遇到短路訊號，表示此量測可能有以下問題：
\     1. 設定參數有問題，啟動位置應該要更靠近工件。
\     2. 量測方向錯誤。
\     3. 短路訊號有問題。

\ direction: +1 是 +X，-1 是 -X，+2 是 +Y，-2 是 -Y，+3 是 +Z，-3 是 -Z。
\ distance: 為正數。
: edge-meas ( direction -- ) ( F: distance -- )
    +coordinator meas-group group! +group \ 啟動量測軸組
    \ 決定量測方向
    dup 0< if -1.0e else 1.0e then fswap fover f* abs ( axis ) ( F: dir s-dis )
    0detect-records \ 清空偵測紀錄
    -touch-ignore
    dup detect-v1 f@ move-by-distance  ( axis ) ( F: dir )
    \ 紀錄量測基準點
    dup mcs-p@ base-pos f! ( axis ) ( F: dir )

    search-error? not if
        detect-times @ 0 ?do
            +touch-ignore
            \ 後退
            dup fdup reverse-pitch f@ f* back-rate f@ move-by-distance ( axis ) ( F: dir )
            -touch-ignore
            \ 再次檢測
            dup fdup detect-pitch f@ f* base-pos f@ dup mcs-p@ f- f+ detect-v2 f@ move-by-distance ( axis ) ( F: dir )
            search-error? if
                leave
            else
                \ 偵測到量測邊界才做紀錄
                dup mcs-p@ fdup i detect-records fparam! \ 紀錄偵測位置
                fswap
            then
        loop

        search-error? if
            0stacks
            reset-job
            .edge-meas-search-error
        else
            drop fdrop
            \ 計算量測結果
            edge-result
        then
    else
        0stacks
        reset-job
        .edge-meas-search-error
    then
    edge-meas-end
;

\ 執行碰邊量測
\ direction: +1 是 +X，-1 是 -X，+2 是 +Y，-2 是 -Y，+3 是 +Z，-3 是 -Z。
\ distance: 為正數。
: start-edge-meas ( direction -- ) ( F: distance -- )
    meas-allowed?
    if
        serving motion-status !         \ 切換到 serving
        meas PECM-motion-state !         \ 設定為 meas
        ." log|Measuring" cr
        \ 進行碰邊量測
        edge-meas
    else
        drop fdrop
        ." error|Measuring failed" cr
        .meas-errors
    then
;

\ step1 紀錄起始位置並往x+方向尋邊
\ step2 倘若有尋到邊,回到x起始點,並往x-方向尋邊
\ step3 倘若有尋到邊,由x+與x-計算中心點,並到中心點後往y+方向尋邊
\ step4 倘若有尋到邊,回到y起始點,並往y-方向尋邊
\ step5 倘若有尋到邊,由y+與y-計算中心點,並回到中心點後,在最後一次往x+方向碰邊
\ step6 倘若有尋到邊,更新x+方向並回到中心點後往x-方向碰邊
\ step7 倘若有尋到邊,由x+與x-計算中心點,並到中心點,此點為孔中心

: center-meas
    +coordinator meas-group group! +group \ 啟動量測軸組
    
    \  --------------step1------------
    \ 紀錄起始位置 
    1 mcs-p@ centerX f! 2 mcs-p@ centerY f! 
    \ 先往+x軸方向,以量測孔中心的速度往+x軸方向
    -touch-ignore
    1 diameter f@ center-rate f@ move-by-distance
    \ 紀錄X+方向的碰觸點
    1 pcs-p@ centerX+ f!

    \ ---------------step2-------------
    \ 如果有碰到邊 就快速回到初始點 並且以尋孔中心的速度往-x軸方向
    search-error? not if
        \ 開啟忽略碰邊回到初始點
        +touch-ignore
        1 centerX f@ centerX+ f@ f-  detect-v1 f@ move-by-distance

        \ 關閉短路無視 並往-x軸方向
        -touch-ignore
        1 diameter f@ fnegate center-rate f@ move-by-distance
        \ 紀錄X-方向的碰觸點
        1 pcs-p@ centerX- f!
    then

    \ ---------------step3--------------
    \ 如果有碰到邊 就更新centerX 其中centerX = (centerX+ + centerX-)/2
    \ 並回到x軸的中心點centerX
    \ 回到中心點後往Y+方向尋邊
    search-error? not if
        centerX+ f@ centerX- f@ f+ 2e f/ centerX f!
        +touch-ignore
        1 centerX f@ centerX- f@ f- detect-v1 f@ move-by-distance

        \ 關閉短路無視,以量測孔中心的速度往+y方向
        -touch-ignore
        2 diameter f@ center-rate f@ move-by-distance
        \ 紀錄Y+方向的碰觸點
        2 pcs-p@ centerY+ f!
    then

    \ ---------------step4--------------
    \ 如果有碰到邊 就快速回到y初始點 並且以尋孔中心的速度往-y軸方向
    search-error? not if
        \ 開啟忽略碰邊回到初始點
        +touch-ignore
        2 centerY f@ centerY+ f@ f-  detect-v1 f@ move-by-distance

        \ 關閉短路無視 並往-Y軸方向
        -touch-ignore
        2 diameter f@ fnegate center-rate f@ move-by-distance
        \ 紀錄Y-方向的碰觸點
        2 pcs-p@ centerY- f!
    then

    \ ---------------step5--------------
    \ 如果有碰到邊 就更新centerY 其中centerY = (centerY+ + centerY-)/2
    \ 並回到Y軸的中心點centerY
    search-error? not if
        \ 計算y軸中心點 並短路無視回到y軸中心點
        centerY+ f@ centerY- f@ f+ 2e f/ centerY f!
        +touch-ignore
        2 centerY f@ centerY- f@ f- detect-v1 f@ move-by-distance
        
        \ 重複在最後碰x軸方向
        \ 關閉短路無視 並往x+方向在移動一次並紀錄
        -touch-ignore
        1 diameter f@ center-rate f@ move-by-distance
        \ 紀錄X+方向的碰觸點
        1 pcs-p@ centerX+ f!
    then

    \ ---------------step6-------------
    \ 如果有碰到邊 就快速回到x軸中心點 並且以尋孔中心的速度往-x軸方向
    search-error? not if
        \ 開啟忽略碰邊回到初始點
        +touch-ignore
        1 centerX f@ centerX+ f@ f-  detect-v1 f@ move-by-distance

        \ 關閉短路無視 並往-x軸方向
        -touch-ignore
        1 diameter f@ fnegate center-rate f@ move-by-distance
        \ 紀錄X-方向的碰觸點
        1 pcs-p@ centerX- f!
    then
    \ ---------------step7--------------
    \ 如果有碰到邊 就更新centerX 其中centerX = (centerX+ + centerX-)/2
    \ 並回到x軸的中心點centerX 此點為孔中心,結束後需關閉碰邊無視
    search-error? not if
        centerX+ f@ centerX- f@ f+ 2e f/ centerX f!
        +touch-ignore
        1 centerX f@ centerX- f@ f- detect-v1 f@ move-by-distance

        \ 已結束,短路有視,並更新旗標,發出鳴聲告知使用者已結束
        -touch-ignore
        search-center-done? on
    then


    search-center-done? @ if
        search-ending-buzzer set-buzzer
    else
        search-error? if
            0stacks
            reset-job
            .edge-meas-search-error
        then
    then

    edge-meas-end

;

\ 執行量測孔中心 需輸入直徑
\ distance: 為正數。
: start-center ( F: diameter -- )
    meas-allowed?
    if
        serving motion-status !         \ 切換到 serving
        meas PECM-motion-state !         \ 設定為 meas
        ." log|Start center measure" cr
        diameter f!
        \ 進行碰邊量測
        search-center-done? off
        center-meas
    else
        fdrop
            ." error|Start center measure failed" cr
        .meas-errors
    then
;

\ direction: +1 是 +X，-1 是 -X，+2 是 +Y，-2 是 -Y。
\ distance col-distance: 為正數。

: col-par-setting ( direction -- ) ( F: dx dy dz -- )
    3 col-distance fparam! 2 col-distance fparam! 1 col-distance fparam!
    case
        \ 先移動+x軸
        1  of
            1 1 col-axis param!  1 1 col-dir param!
            1 2 col-axis param! -1 2 col-dir param!
            2 3 col-axis param!  1 3 col-dir param!
            2 4 col-axis param! -1 4 col-dir param!
        endof
        \ 先移動-x軸
        -1  of
            1 1 col-axis param! -1 1 col-dir param!
            1 2 col-axis param!  1 2 col-dir param!
            2 3 col-axis param!  1 3 col-dir param!
            2 4 col-axis param! -1 4 col-dir param!
        endof
        \ 先移動+y軸
        2  of
            2 1 col-axis param!  1 1 col-dir param!
            2 2 col-axis param! -1 2 col-dir param!
            1 3 col-axis param!  1 3 col-dir param!
            1 4 col-axis param! -1 4 col-dir param!
        endof
        \ 先移動-y軸
        -2  of
            2 1 col-axis param! -1 1 col-dir param!
            2 2 col-axis param!  1 2 col-dir param!
            1 3 col-axis param!  1 3 col-dir param!
            1 4 col-axis param! -1 4 col-dir param!
        endof
    endcase
;

: column-meas
    +coordinator meas-group group! +group \ 啟動量測軸組
    \  --------------step1------------
    \ 紀錄起始位置 
    1 mcs-p@ 1 col-temp-p fparam! 2 mcs-p@ 2 col-temp-p fparam!
    \ 依據step1設定往設定軸方向,以量測柱心的速度往第一個設定軸方向移動
    -touch-ignore
    1 col-axis param@ 1 col-dir param@ s>f 100e mm f* column-rate f@ move-by-distance
    \ 紀錄第一次的碰觸點
    1 col-axis param@ pcs-p@ 1 col-touch-p fparam!
    

    \ ---------------step2-------------
    \ 如果有碰到邊,就快速回到初始點,回到初始點後,快速將z軸拉起(移動距離3 col-distance)
    \ 拉起後快速移動設定軸(1 col-axis param@ col-distance)
    \ 移動完設定軸後將z軸放下,並量測設定軸的另一側邊
    search-error? not if
        \ 開啟忽略碰邊回到初始點
        +touch-ignore
        1 col-axis param@ dup col-temp-p fparam@ dup pcs-p@ f- detect-v1 f@ move-by-distance
        \ 關閉短路無視 並由z軸方向拉起
        -touch-ignore
        3 3 col-distance fparam@ detect-v1 f@ move-by-distance
        \ 移動設定軸到另一側
        1 col-axis param@ 1 col-dir param@ s>f dup col-distance fparam@ f* detect-v1 f@ move-by-distance
        \ z軸降下
        3 3 col-distance fparam@ fnegate detect-v1 f@ move-by-distance
        
        \ 碰邊設定軸的另一側
        \ 紀錄起始位置 
        1 mcs-p@ 1 col-temp-p fparam! 2 mcs-p@ 2 col-temp-p fparam!
        2 col-axis param@ 2 col-dir param@ s>f 100e mm f* column-rate f@ move-by-distance
        \ 紀錄第二次的碰觸點
        2 col-axis param@ pcs-p@ 2 col-touch-p fparam!
    then

    \ ---------------step3-------------
    \ 如果有碰到邊,就由兩次碰邊結果計算中心點 (由touch1和touch2平均 即為中心點)
    \ 快速回到初始點,回到初始點後,快速將z軸拉起(移動距離3 col-distance)
    \ 並回到計算的中心點準備量測另一軸 
    \ 準備量測第二軸 移動第二軸到量測物的側邊
    \ 到達側邊後 紀錄起始位置 開始進行量測
    search-error? not if
        \ 計算第一軸的中心點
        1 col-touch-p fparam@ 2 col-touch-p fparam@ f+ 2e f/ 1 col-axis param@ col-center-p fparam!
        +touch-ignore
        \ 回到起始位置
        2 col-axis param@ dup col-temp-p fparam@ dup pcs-p@ f- detect-v1 f@ move-by-distance
        -touch-ignore
        \ z軸拉起
        3 3 col-distance fparam@ detect-v1 f@ move-by-distance
        \ 移動到第一軸的中心點
        2 col-axis param@ dup col-center-p fparam@ dup pcs-p@ f- detect-v1 f@ move-by-distance

        \ 準備第三次量測 量測第二軸 移動第二軸到量測物的側邊
        3 col-axis param@ 3 col-dir param@ s>f dup col-distance fparam@ f* fnegate 2e f/ detect-v1 f@ move-by-distance
        \ z軸降下
        3 3 col-distance fparam@ fnegate detect-v1 f@ move-by-distance
        
        \ 紀錄起始位置 
        1 mcs-p@ 1 col-temp-p fparam! 2 mcs-p@ 2 col-temp-p fparam!
        \ 進行第三次量測 移動第二軸方向,以量測柱心的速度往第二個軸方向移動
        -touch-ignore
        3 col-axis param@ 3 col-dir param@ s>f 100e mm f* column-rate f@ move-by-distance
        \ 紀錄第三次的碰觸點
        3 col-axis param@ pcs-p@ 3 col-touch-p fparam!
    then

    \ ---------------step4-------------
    \ 尋找第二軸的另一側邊
    \
    search-error? not if
        \ 開啟忽略碰邊回到初始點
        +touch-ignore
        3 col-axis param@ dup col-temp-p fparam@ dup pcs-p@ f- detect-v1 f@ move-by-distance
        -touch-ignore
        \ z軸拉起
        3 3 col-distance fparam@ detect-v1 f@ move-by-distance
        \ 準備第四次量測 量測第二軸 移動第二軸到量測物的側邊
        4 col-axis param@ 4 col-dir param@ s>f dup col-distance fparam@ f* fnegate  detect-v1 f@ move-by-distance
        \ z軸降下
        3 3 col-distance fparam@ fnegate detect-v1 f@ move-by-distance
        \ 進行第四次量測 移動第二軸方向,以量測柱心的速度往第二個軸方向移動
        -touch-ignore
        1 mcs-p@ 1 col-temp-p fparam! 2 mcs-p@ 2 col-temp-p fparam!
        4 col-axis param@ 4 col-dir param@ s>f 100e mm f* column-rate f@ move-by-distance
        \ 紀錄第四次的碰觸點
        4 col-axis param@ pcs-p@ 4 col-touch-p fparam!
    then

    \ ---------------step5-------------
    \ 如果有碰到邊,就由兩次碰邊結果計算中心點 (由touch3和touch4平均 即為中心點)
    \ 快速回到初始點,回到初始點後,快速將z軸拉起(移動距離3 col-distance)
    \ 並回到計算的中心點準備量測另一軸 
    \ 準備量測第二軸 移動第二軸到量測物的側邊
    \ 到達側邊後 紀錄起始位置 開始進行量測
    search-error? not if
        \ 計算第二軸的中心點
        3 col-touch-p fparam@ 4 col-touch-p fparam@ f+ 2e f/ 4 col-axis param@ col-center-p fparam!
        +touch-ignore
        \ 回到起始位置
        4 col-axis param@ dup col-temp-p fparam@ dup pcs-p@ f- detect-v1 f@ move-by-distance
        -touch-ignore
        \ z軸拉起
        3 3 col-distance fparam@ detect-v1 f@ move-by-distance
        \ 移動到第二軸的中心點
        4 col-axis param@ dup col-center-p fparam@ dup pcs-p@ f- detect-v1 f@ move-by-distance
    
        \ 重新量測第一軸 先移動到第一軸的側邊
        1 col-axis param@ 1 col-dir param@ s>f dup col-distance fparam@ f* fnegate 2e f/ detect-v1 f@ move-by-distance
        \ z軸降下
        3 3 col-distance fparam@ fnegate detect-v1 f@ move-by-distance
        
        \ 紀錄暫停位置
        1 mcs-p@ 1 col-temp-p fparam! 2 mcs-p@ 2 col-temp-p fparam!
        \ 依據設定往設定軸方向,以量測柱心的速度往第一個設定軸方向移動
        1 col-axis param@ 1 col-dir param@ s>f 100e mm f* column-rate f@ move-by-distance
        \ 紀錄第五次的碰觸點
        1 col-axis param@ pcs-p@ 1 col-touch-p fparam!
    then

    \ ---------------step6-------------
    \ 如果有碰到邊,就快速回到初始點,回到初始點後,快速將z軸拉起(移動距離3 col-distance)
    \ 拉起後快速移動設定軸(1 col-axis param@ col-distance)
    \ 移動完設定軸後將z軸放下,並量測設定軸的另一側邊
    search-error? not if
        \ 開啟忽略碰邊回到初始點
        +touch-ignore
        1 col-axis param@ dup col-temp-p fparam@ dup pcs-p@ f- detect-v1 f@ move-by-distance
        \ 關閉短路無視 並由z軸方向拉起
        -touch-ignore
        3 3 col-distance fparam@ detect-v1 f@ move-by-distance
        \ 移動設定軸到另一側
        1 col-axis param@ 1 col-dir param@ s>f dup col-distance fparam@ f* detect-v1 f@ move-by-distance
        \ z軸降下
        3 3 col-distance fparam@ fnegate detect-v1 f@ move-by-distance
        
        \ 碰邊設定軸的另一側
        \ 紀錄起始位置 
        1 mcs-p@ 1 col-temp-p fparam! 2 mcs-p@ 2 col-temp-p fparam!
        2 col-axis param@ 2 col-dir param@ s>f 100e mm f* column-rate f@ move-by-distance
        \ 紀錄第六次的碰觸點
        2 col-axis param@ pcs-p@ 2 col-touch-p fparam!
    then

    \ ---------------step7-------------
\ 如果有碰到邊,就由兩次碰邊結果計算中心點 (由touch1和touch2平均 即為中心點)
    \ 快速回到初始點,回到初始點後,快速將z軸拉起(移動距離3 col-distance)
    \ 並回到計算的中心點準備量測另一軸 
    \ 準備量測第二軸 移動第二軸到量測物的側邊
    \ 到達側邊後 紀錄起始位置 開始進行量測
    search-error? not if
        \ 再次計算第一軸的中心點
        1 col-touch-p fparam@ 2 col-touch-p fparam@ f+ 2e f/ 1 col-axis param@ col-center-p fparam!
        +touch-ignore
        \ 回到起始位置
        2 col-axis param@ dup col-temp-p fparam@ dup pcs-p@ f- detect-v1 f@ move-by-distance
        -touch-ignore
        \ z軸拉起
        3 3 col-distance fparam@ detect-v1 f@ move-by-distance
        \ 移動到第一軸的中心點 此點為真正的中心點
        2 col-axis param@ dup col-center-p fparam@ dup pcs-p@ f- detect-v1 f@ move-by-distance
        search-center-done? on
    then


    search-center-done? @ if
        search-ending-buzzer set-buzzer
    else
        search-error? if
            0stacks
            reset-job
            .edge-meas-search-error
        then
    then

    edge-meas-end
;

: start-column ( direction -- ) ( F: dx dy dz -- )
    meas-allowed?
    if
        serving motion-status !         \ 切換到 serving
        meas PECM-motion-state !         \ 設定為 meas
        ." log| Start column measure " cr
        col-par-setting
        \ 進行柱心量測
        search-column-done? off
        column-meas
    else
        drop
        fdrop fdrop fdrop
            ." error| Start column measure failed" cr
        .meas-errors
    then
;
\ ===== move sfc =====
\ 說明：
\   當收到move-go,則啟動此sfc,
\   會根據move-axis,move-dis,move-v做設定,並開始移動,結束後關閉move-go旗標
\   當結束或緊急停止,此時meas-group group! gstop? = true 
\   後關閉move-go旗標結束此sfc
\
\ SFC：
\
\
\               move-idle
\                   |
\                   +   move-go?
\                   |
\                   v
\               move-init
\                   |
\                   +   move-init-ready?
\                   |
\                   v
\               move-waiting-to-start
\                   |
\                   +   move-start-ready?
\                   |
\                   v
\               move-waiting-to-end
\                   |
\                   +   move-ending?
\                   |
\                   v
\               move-ending
\                   |
\                   +   move-true-1
\                   |
\                   v
\               move-idle

variable move-go
variable move-axis
fvariable move-dis
fvariable move-v

\ 等待move-go開啟,表示開始運作
: move-idle
    ( do nothing )
;

\ 根據設定的移動軸,距離,速度開始運動
: move-init
    move-axis @ move-dis f@ move-v f@
    meas-group group! gstart \ 啟動量測軸組
    0path 1 pcs-p@ 2 pcs-p@ 3 pcs-p@ move3d
    feedrate@ fswap feedrate!    ( F: distance feedrate-old )
    fswap target-pos! target-pos@ line3d
    feedrate!
;

\ 在此須等待2ms,等待軸組正式移動
: move-waiting-to-start
    ( do nothing )
;

\ 在此等待軸組結束運動或碰觸到而停下
: move-waiting-to-end
    ( do nothing )
;

\ 結束後將move-go旗標關閉
: move-ending
    move-go off
;

step move-idle
step move-init
step move-waiting-to-start
step move-waiting-to-end
step move-ending

: move-go?
    move-go @
;
: move-init-ready?
    true
;
: move-start-ready?
    ['] move-waiting-to-start elapsed 3 >
;
: move-ending?
    meas-group group! gstop?
;
: move-true-1
    true
;
transition move-go?
transition move-init-ready?
transition move-start-ready?
transition move-ending?
transition move-true-1
\ ==================== links ====================
' move-idle                     ' move-go?                  -->
' move-go?                      ' move-init                 -->
' move-init                     ' move-init-ready?          -->
' move-init-ready?              ' move-waiting-to-start     -->
' move-waiting-to-start         ' move-start-ready?         -->
' move-start-ready?             ' move-waiting-to-end       -->
' move-waiting-to-end           ' move-ending?              -->
' move-ending?                  ' move-ending               -->
' move-ending                   ' move-true-1               -->
' move-true-1                   ' move-idle                 -->

\ ===== 碰邊量測sfc =====
\ 說明：
\   當收到edge-meas-go 則啟動此sfc
\   會依據 移動的軸,距離,速度分別設定 move-axis,move-dis,move-v
\   並立起旗標move-go 啟動move的sfc
\
\ 正常流程:
\   開啟短路有視，使用第一段速移動到量測基準位罝。
\   由量測次數重覆量測。
\   重覆量測動作：到達量測基本位罝後, 使用後退速沿指定量測方向退指定距離。
\                 再由第二段速（慢速）前進指定量測距離，並紀錄量測到的位置。
\   最後將紀錄量測到的位置，計算量測位置的平均值與差異值。
\
\ 異常流程:
\ 沿循邊方向運動 distance 距離後沒有遇到短路訊號，表示此量測可能有以下問題：
\     1. 設定參數有問題，啟動位置應該要更靠近工件。
\     2. 量測方向錯誤。
\     3. 短路訊號有問題。   
\ 緊急開關按下時，會使得motion-status != serving，也會中途停止量測動作並回到edge-meas-init
\
\ SFC：
\
\
\            edge-meas-init
\                   |
\                   +   edge-meas-go?
\                   |
\                   v
\           edge-meas-wait-1
\                   +-----------------------------------+
\                   |                                   |
\                   +   edge-meas-allow?                +   edge-meas-not-allow?
\                   |                                   |
\                   v                                   v
\           edge-meas-base-setting                  edge-meas-error
\                   |                                   |
\                   +   edge-meas-true-1                +   edge-meas-true-2
\                   |                                   |
\                   v                                   v
\           edge-meas-wait-move-1                   edge-meas-init
\                   |
\                   +   edge-meas-move-end-1?
\                   |
\                   v
\               edge-meas-wait-2
\                   +-----------------------------------+
\                   |                                   |
\                   +   edge-meas-search-not-error?     +   edge-meas-search-error?
\                   |                                   |
\                   v                                   v
\               edge-meas-record-base-pos            edge-meas-error-reset
\                   |                                   |
\                   |                                   +   edge-meas-true-3
\                   |                                   |
\                   |                                   v
\                   |                               edge-meas-init
\                   |
\                   +-----------------------------------+
\                   |                                   |
\                   +   edge-meas-<=detect-times?       +   edge-meas->detect-times?
\                   |                                   |
\                   v                                   v
\               edge-meas-back-setting              edge-meas-ending
\                   |                                   |
\                   +   edge-meas-true-4                +   edge-meas-true-7
\                   |                                   |
\                   v                                   v
\               edge-meas-back-wait                 edge-meas-init
\                   |
\                   +   edge-meas-move-end-2?
\                   |
\                   v
\               edge-meas-wait-3
\                   +-----------------------------------+
\                   |                                   |
\                   +   edge-meas-move-not-failed-1?    +    edge-meas-move-failed-1?
\                   |                                   |
\                   v                                   v                   
\               edge-meas-forward-setting           edge-meas-error-reset
\                   |
\                   +   edge-meas-true-5
\                   |
\                   v
\               edge-meas-wait-move-2
\                   |
\                   +   edge-meas-move-end-3?
\                   |
\                   v
\               edge-meas-wait-4
\                   +-----------------------------------+
\                   |                                   |
\                   +   edge-meas-move-not-failed-2?    +    edge-meas-move-failed-2?
\                   |                                   |
\                   v                                   v 
\               edge-meas-record-pos                edge-meas-error-reset
\                   |
\                   +   edge-meas-true-6
\                   |
\                   v
\               edge-meas-wait-2

variable edge-meas-count

: edge-meas-init
    ( do nothing )
;
: edge-meas-wait-1
    ( do nothing )
;

\ 切換狀態與啟動move-sfc
: edge-meas-base-setting
    serving motion-status !         \ 切換到 serving
    meas PECM-motion-state !         \ 設定為 meas
    +coordinator meas-group group! 0path +group
    0 edge-meas-count !
    0detect-records
    edge-meas-axis @ move-axis !
    edge-meas-dis f@ move-dis f!
    detect-v1 f@ move-v f!
    move-go on
;
\ 關閉量測旗標與報錯
: edge-meas-error
    edge-meas-go off
    .meas-errors
;
: edge-meas-wait-move-1
    ( do nothing )
;
: edge-meas-wait-2
    ( do nothing )
;

: edge-meas-wait-3
    ( do nothing )
;
: edge-meas-wait-4
    ( do nothing )
;
\ 未搜尋到邊緣則報訊息與結束工作
: edge-meas-error-reset
    0stacks
    reset-job
    .edge-meas-search-error
    edge-meas-end
    edge-meas-go off
;

\ 紀錄base-pos
: edge-meas-record-base-pos
    move-axis @ mcs-p@ base-pos f!
    1 edge-meas-count +!
;

\ 完成工作則印出結果與結束量測
: edge-meas-ending
    detect-times @ 0 do
        i detect-records fparam@
    loop
    edge-result
    edge-meas-end
    edge-meas-go off
;

\ 做後退的動作:
\ 開啟短路無視,其中move-dis需根據edge-meas-dis的方向,決定後退距離是否要*-1
: edge-meas-back-setting
    +touch-ignore
    edge-meas-dis f@ 0e f> if
       reverse-pitch f@ move-dis f!
    else
       reverse-pitch f@ -1e f* move-dis f!
    then
    back-rate f@ move-v f!
    move-go on
;

\ 等待後退
: edge-meas-back-wait
    ( do nothing )
;

\ 做精密量測的動作:
\ 關閉短路無視,其中move-v需以慢速移動(detect-v2)
\ move-dis = base-pos - ( move-axis @ mcs-p@ ) + detect-pitch*sign(edge-meas-dis)
: edge-meas-forward-setting
    -touch-ignore
    edge-meas-dis f@ 0e f> if
       detect-pitch f@ base-pos f@ move-axis @ mcs-p@ f- f+ move-dis f!
    else
       detect-pitch f@ -1e f* base-pos f@ move-axis @ mcs-p@ f- f+ move-dis f!
    then
    detect-v2 f@ move-v f!
    move-go on
;

\ 等待前進結束
: edge-meas-wait-move-2
    ( do nothing )
;

\ 紀錄位置
: edge-meas-record-pos
    move-axis @ mcs-p@ edge-meas-count @ 1- detect-records fparam!
;

step edge-meas-init
step edge-meas-wait-1
step edge-meas-base-setting
step edge-meas-error
step edge-meas-wait-move-1
step edge-meas-wait-2
step edge-meas-wait-3
step edge-meas-wait-4
step edge-meas-error-reset
step edge-meas-record-base-pos
step edge-meas-ending
step edge-meas-back-setting
step edge-meas-back-wait
step edge-meas-forward-setting
step edge-meas-wait-move-2
step edge-meas-record-pos

: edge-meas-go?
    edge-meas-go @ 
;
: edge-meas-allow?
    meas-allowed?
;
: edge-meas-not-allow?
    edge-meas-allow? not
;

: edge-meas-true-1
    true
;
: edge-meas-true-2
    true
;
: edge-meas-move-end-1?
    move-go @ not
;
: edge-meas-search-error?
    search-error?
    motion-status @ serving <> or
;
: edge-meas-true-3
    true
;
: edge-meas-search-not-error?
    edge-meas-search-error? not
;
: edge-meas-<=detect-times?
    edge-meas-count @ detect-times @ <=
;
: edge-meas->detect-times? 
    edge-meas-<=detect-times? not
;
: edge-meas-true-4
    true
;
: edge-meas-move-end-2?
    move-go @ not
;
: edge-meas-true-5
    true
;
: edge-meas-move-end-3?
    move-go @ not
;
: edge-meas-true-6
    true
;
: edge-meas-true-7
    true
;
: edge-meas-move-not-failed-1?
    motion-status @ serving =
;
: edge-meas-move-failed-1?
    edge-meas-move-not-failed-1? not
;
: edge-meas-move-not-failed-2?
    motion-status @ serving =
;
: edge-meas-move-failed-2?
    edge-meas-move-not-failed-2? not
;
transition edge-meas-go?
transition edge-meas-allow?
transition edge-meas-not-allow?
transition edge-meas-true-1
transition edge-meas-true-2
transition edge-meas-move-end-1?
transition edge-meas-search-error?
transition edge-meas-true-3
transition edge-meas-search-not-error?
transition edge-meas-<=detect-times?
transition edge-meas->detect-times?
transition edge-meas-true-4
transition edge-meas-move-end-2?
transition edge-meas-true-5
transition edge-meas-move-end-3?
transition edge-meas-true-6
transition edge-meas-true-7
transition edge-meas-move-not-failed-1?
transition edge-meas-move-failed-1?
transition edge-meas-move-not-failed-2?
transition edge-meas-move-failed-2?

\ ==================== links ====================
' edge-meas-init                    ' edge-meas-go?                     -->
' edge-meas-go?                     ' edge-meas-wait-1                  -->
' edge-meas-wait-1                  ' edge-meas-allow?                  -->
' edge-meas-allow?                  ' edge-meas-base-setting            -->
' edge-meas-base-setting            ' edge-meas-true-1                  -->
' edge-meas-true-1                  ' edge-meas-wait-move-1             -->
' edge-meas-wait-move-1             ' edge-meas-move-end-1?             -->
' edge-meas-move-end-1?             ' edge-meas-wait-2                  -->

' edge-meas-wait-2                  ' edge-meas-search-error?           -->
' edge-meas-search-error?           ' edge-meas-error-reset             -->
' edge-meas-error-reset             ' edge-meas-true-3                  -->
' edge-meas-true-3                  ' edge-meas-init                    -->

' edge-meas-wait-2                  ' edge-meas-search-not-error?       -->
' edge-meas-search-not-error?       ' edge-meas-record-base-pos         -->
' edge-meas-record-base-pos         ' edge-meas-<=detect-times?         -->
' edge-meas-<=detect-times?         ' edge-meas-back-setting            -->
' edge-meas-back-setting            ' edge-meas-true-4                  -->
' edge-meas-true-4                  ' edge-meas-back-wait               -->
' edge-meas-back-wait               ' edge-meas-move-end-2?             -->
' edge-meas-move-end-2?             ' edge-meas-wait-3                  -->

' edge-meas-wait-3                  ' edge-meas-move-failed-1?          -->
' edge-meas-move-failed-1?          ' edge-meas-error-reset             -->

' edge-meas-wait-3                  ' edge-meas-move-not-failed-1?      -->
' edge-meas-move-not-failed-1?      ' edge-meas-forward-setting         -->
' edge-meas-forward-setting         ' edge-meas-true-5                  -->
' edge-meas-true-5                  ' edge-meas-wait-move-2             -->
' edge-meas-wait-move-2             ' edge-meas-move-end-3?             -->
' edge-meas-move-end-3?             ' edge-meas-wait-4                  -->

' edge-meas-wait-4                  ' edge-meas-move-failed-2?          -->
' edge-meas-move-failed-2?          ' edge-meas-error-reset             -->

' edge-meas-wait-4                  ' edge-meas-move-not-failed-2?      -->
' edge-meas-move-not-failed-2?      ' edge-meas-record-pos              -->
' edge-meas-record-pos              ' edge-meas-true-6                  -->
' edge-meas-true-6                  ' edge-meas-wait-2                  -->

' edge-meas-record-base-pos         ' edge-meas->detect-times?          -->
' edge-meas->detect-times?          ' edge-meas-ending                  -->
' edge-meas-ending                  ' edge-meas-true-7                  -->
' edge-meas-true-7                  ' edge-meas-init                    -->


' edge-meas-wait-1                  ' edge-meas-not-allow?              -->
' edge-meas-not-allow?              ' edge-meas-error                   -->
' edge-meas-error                   ' edge-meas-true-2                  -->
' edge-meas-true-2                  ' edge-meas-init                    -->

marker -work
