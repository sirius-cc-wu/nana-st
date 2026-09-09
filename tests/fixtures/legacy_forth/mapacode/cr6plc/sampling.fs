-work

variable sampling-clock
variable sampling-size 50 sampling-size !
variable buzzer-op
variable buzzer-clock

create cr6-level-samples sampling-size @ dup , 0 , falign floats allot
create cr6-pH-samples sampling-size @ dup , 0 , falign floats allot

: cap@ ( addr -- cap)
    @
;

: idx ( addr -- idx-addr )
    1 cells +
;

: element ( addr -- element-addr )
    2 cells + faligned
;

\ 得到當前指標指向的資料，不移動指標
: fpop ( addr -- )( F: -- val )
    dup idx @ floats                ( addr idx*floats )
    swap element + f@               ( F: val )
;

\ 存入一筆資料後指標向前一格
: fpush ( addr -- )( F: val -- )
    dup idx @ floats                ( addr idx*floats )( F: val )
    over element + f!               ( addr )
    dup idx @ 1+ over cap@ mod      ( addr idx' )
    swap idx !                      ( )
;

: cr6-level-s@ ( F: -- val:m )
    \ TODO 換算成實際水位
    \ 假設探棒總長 L=2m
    \ 假設探頭距槽底 0.1m
    cr6-level-ain ec-ain@ s>f
    2e 15e f** 1e f- f/
    2e f*
    0.1e f+
;

: cr6-pH-s@ ( F: -- pH )
    cr6-pH-ain ec-ain@ s>f
    2e 15e f** 1e f- f/
    14e f*
;

: cr6-level-forth ( -- )
    cr6-level f@ cr6-level-samples fpop sampling-size @ s>f f/ f-
    cr6-level-s@ fdup cr6-level-samples fpush
    sampling-size @ s>f f/ f+
    cr6-level f!
;

: cr6-pH-forth ( -- )
    cr6-pH f@ cr6-pH-samples fpop sampling-size @ s>f f/ f-
    cr6-pH-s@ fdup cr6-pH-samples fpush
    sampling-size @ s>f f/ f+
    cr6-pH f!
;

: +buzzer ( -- )
    true buzzer-op !
    buzzer-clock renew
;

: -buzzer ( -- )
    false buzzer-op !
    buzzer-dout off
;

: buzzer-forth ( -- )
    buzzer-op @
    if
        buzzer-dout ec-dout@
        if
            mtime buzzer-clock @ - abs 500 >
            if
                buzzer-dout off
                buzzer-clock renew
            then
        else
            mtime buzzer-clock @ - abs 3000 >
            if
                buzzer-dout on
                buzzer-clock renew
            then
        then
    then
;



\ =============== SFC ===============
: sampling-loop ( -- )
    mtime sampling-clock @ - abs 50 >
    if
        cr6-level-forth
        cr6-pH-forth
        buzzer-forth
        sampling-clock renew
    then
;
step sampling-loop



marker -work