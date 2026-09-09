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
    ECM-power-state @                       \ power on
    ECM-motion-state @ idle = and           \ 且 motion idle
    feedhold? not and                       \ 且 not in feedhold
    is-EMS? not and                         \ 且緊急停止未觸發
    ECM-on-sl? not and                      \ 且未碰觸軟體極限
    is-short? not and                       \ 且無量測短路
    v-axis-corrected? and                   \ 且 V 軸已回正
;

\ 印出無法進行量測之異警
: .meas-errors ( -- )
    ECM-power-state @ not
    if
        ." error|Not power on. Start measuring failed.;A1301" cr
    then
    ECM-motion-state @ idle <>
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
    ECM-on-sl?
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
    v-axis-corrected? not
    if
        ." error|V axis position not returned. Start measuring failed.;A1308" cr
    then
;
: .search-errors-msg
    is-EMS?
    if
        ." error|In EMS. Search failed.;A1309" cr
    then
    ECM-on-sl?
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
        meas ECM-motion-state !         \ 設定為 meas
        ." log|Measuring" cr
        \ 進行碰邊量測
        edge-meas
    else
        drop fdrop
        ." error|Measuring failed" cr
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
    meas ECM-motion-state !         \ 設定為 meas
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
