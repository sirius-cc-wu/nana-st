1 6 2constant uart-ch-slv

: charCR 13 ;
: char$ 36 ;
: char- 45 ;
: char0 48 ;
: char1 49 ;
: char2 50 ;
: char3 51 ;
: char4 52 ;
: char5 53 ;
: char6 54 ;
: char7 55 ;
: char8 56 ;
: char9 57 ;
: char: 58 ;
: char; 59 ;
: char< 60 ;
: char= 61 ;
: char> 62 ;
: char? 63 ;

: capacity@ ( addr -- cap )
    @
;

: len ( addr -- len-addr )
    1 cells +
;

: ptr ( addr -- ptr-addr )
    2 cells +
;

: space@ ( addr -- space )
    dup capacity@ swap len @ -
;

: element ( idx addr -- element-addr )
    swap over dup capacity@ -rot                ( addr cap idx addr )
    ptr @                                       ( addr cap idx ptr )
    + swap mod 3 + cells +                      ( element-addr )
;

: pop ( addr -- val )
    0 over element @ swap                       ( val addr )
    dup len @ 1- over len !                     ( val addr )
    dup ptr @ 1+ over capacity@ mod swap ptr !  ( val )
;

: push ( val addr -- )
    swap over dup len @                         ( addr val addr len )
    swap element !                              ( addr )
    dup len @ 1+ swap len !                     ( addr )
;

: vec. ( addr -- )
    0 over len @                                ( addr 0 len )
    begin                                       ( addr n len )
        over over <                             ( addr n len flag )
    while                                       ( addr n len )
        -rot dup 2 pick                         ( len addr n n addr )
        element @ .                             ( len addr n )
        1+ rot                                  ( addr n+1 len )
    repeat                                      ( addr n len )
    drop drop drop                              ( )
;

: vec-emit ( addr -- )
    0 over len @                                ( addr 0 len )
    begin                                       ( addr n len )
        over over <                             ( addr n len flag )
    while                                       ( addr n len )
        -rot dup 2 pick                         ( len addr n n addr )
        element @ emit                          ( len addr n )
        1+ rot                                  ( addr n+1 len )
    repeat                                      ( addr n len )
    drop drop drop                              ( )
;

: 0vec ( addr -- )
    0 over len !
    0 swap ptr !
;

create res 256 dup , 0 , 0 , cells allot

\ 好像不用送 CR

\ $000002-0004-40
\ $000002-4084-70-:30000190104051049501900?=0000=<00?=00005??=020001<00:4?00000000-45
\ 10 13 36 48 48 48 48 48 50 45 52 48 56 52 45 55 48 45 58 51 48 48 48
\ 48 49 57 48 49 48 52 48 53 49 48 52 57 53 48 49 57 48 48 63 61 48 48
\ 48 48 61 60 48 48 63 61 48 48 48 48 53 63 63 61 48 50 48 48 48 49 60
\ 48 48 58 52 63 48 48 48 48 48 48 48 48 45 52 53 

\ $000002-0011-3>
\ $000002-4091-6>-01020000000000000000072000<000072000<000020008000050018000000200-:1
\ 10 13 36 48 48 48 48 48 50 45 52 48 57 49 45 54 62 45 48 49 48 50 48
\ 48 48 48 48 48 48 48 48 48 48 48 48 48 48 48 48 55 50 48 48 48 60 48
\ 48 48 48 55 50 48 48 48 60 48 48 48 48 50 48 48 48 56 48 48 48 48 53
\ 48 48 49 56 48 48 48 48 48 48 50 48 48 45 58 49

\ $000002-0012-3?
\ $000002-4092-6?-0800005001800050060080500800<00000650100009913000000990900>802?0-00
\ 10 13 36 48 48 48 48 48 50 45 52 48 57 50 45 54 63 45 48 56 48 48 48
\ 48 53 48 48 49 56 48 48 48 53 48 48 54 48 48 56 48 53 48 48 56 48 48
\ 60 48 48 48 48 48 54 53 48 49 48 48 48 48 57 57 49 51 48 48 48 48 48
\ 48 57 57 48 57 48 48 62 56 48 50 63 48 45 48 48

\ $000002-0013-40
\ $000002-4093-70-:30000190104051027021900?=0000?600?=000063?=02000193353:00000000-27
\ 10 13 36 48 48 48 48 48 50 45 52 48 56 52 45 55 48 45 58 51 48 48 48 
\ 48 49 57 48 49 48 52 48 53 49 48 50 55 48 50 49 57 48 48 63 61 48 48 
\ 48 48 63 54 48 48 63 61 48 48 48 48 54 51 63 61 48 50 48 48 48 49 57 
\ 51 51 53 51 58 48 48 48 48 48 48 48 48 45 50 55 

\ $000002-0014-41
\ $000002-4094-71-71040200005002051018:>0:5002>48140000000000000000000000000000000-<6
\ 10 13 36 48 48 48 48 48 50 45 52 48 57 52 45 55 49 45 55 49 48 52 48
\ 50 48 48 48 48 53 48 48 50 48 53 49 48 49 56 58 62 48 58 53 48 48 50
\ 62 52 56 49 52 48 48 48 48 48 48 48 48 48 48 48 48 48 48 48 48 48 48
\ 48 48 48 48 48 48 48 48 48 48 48 48 48 45 60 54  

\ $000002-0021-3?
\ $000002-40:1-6?-00130065000101650001015<015<017<4?0000>0563;00000000000000000000->9
\ 10 13 36 48 48 48 48 48 50 45 52 48 58 49 45 54 63 45 48 48 49 51 48
\ 48 54 53 48 48 48 49 48 49 54 53 48 48 48 49 48 49 53 60 48 49 53 60
\ 48 49 55 60 52 63 48 48 48 48 62 48 53 54 51 59 48 48 48 48 48 48 48
\ 48 48 48 48 48 48 48 48 48 48 48 48 48 45 62 57

: send-req
    char$ 1 uart-ch-slv uart-data!
    char0 1 uart-ch-slv uart-data!
    char0 1 uart-ch-slv uart-data!
    char0 1 uart-ch-slv uart-data!
    char0 1 uart-ch-slv uart-data!
    char0 1 uart-ch-slv uart-data!
    char2 1 uart-ch-slv uart-data!
    char- 1 uart-ch-slv uart-data!
    char0 char0 char2 char1 4 uart-ch-slv uart-data!
    \ char0 1 uart-ch-slv uart-data!
    \ char2 1 uart-ch-slv uart-data!
    \ char1 1 uart-ch-slv uart-data!
    char- 1 uart-ch-slv uart-data!
    char3 char? 2 uart-ch-slv uart-data!
    \ char? 1 uart-ch-slv uart-data!
    \ charCR 1 uart-ch-slv uart-data!
;

: fetch-res
    begin
        uart-ch-slv uart-rx-len@ 0 >
    while
        res space@ 0 >
        if
            1 uart-ch-slv uart-data@ res push
        else
            ." vec out of space!" cr
            exit
        then
    repeat
;

: test-exit
    123 exit 321
;

: test-outer
    ." 1" cr
    test-exit
    ." 2" cr
;