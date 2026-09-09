-work

\ ==================== measure ====================
variable simu-short                         \ 模擬用的量測短路訊號

\ 回傳是否發生模擬的運動中短路
: simu-short? ( -- flag )
    simu-short @
;


\ 模擬發生量測短路
: +simu-short ( -- )
    simu-short on
;

\ 模擬解除量測短路
: -simu-short ( -- )
    simu-short off
;

marker -work
