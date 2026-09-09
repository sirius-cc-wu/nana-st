-work

\ WCS 
97 constant wcs-len
3 constant wcs-axis-len
create wcs falign wcs-len wcs-axis-len * floats allot

: wcs! ( n -- ) ( F: x0 y0 z0 -- )
  wcs-axis-len * floats wcs falign +
  dup 2 floats + f!
  dup 1 floats + f!
  f!
  ;
  
: wcs@ ( n -- ) ( F: -- x0 y0 z0 ) 
  wcs-axis-len * floats wcs falign +
  dup f@
  dup 1 floats + f@
  2 floats + f@
  ;  

\ program line
variable line#
: line! ( n -- ) line# ! ;
: line@ ( -- n) line# @ ;

: reset-trj 1 group! 0path +group ;

: wait-trj-end  begin 1 group! gend? not while pause repeat pause ;

: dummy-cmd ;
variable end-of-nc-xt
' dummy-cmd end-of-nc-xt !  \ 預設為 dummy-cmd

: end-of-pecm-program
  wait-trj-end
  reset-trj 1 group! -group stop-job
  end-of-nc-xt @ execute
  ." end-of-pecm-program|ok"
  ;

\ target position
create target-px falign 1 floats allot
create target-py falign 1 floats allot
create target-pz falign 1 floats allot
: px! target-px f! ;
: py! target-py f! ;
: pz! target-pz f! ;
: px@ target-px f@ ;
: py@ target-py f@ ;
: pz@ target-pz f@ ;


\ G00/01
variable line-mode
variable line-id

\ line mode 內容:
\ bit0 : cut
0 constant cut-bit
\ bit1 : touch ignore
1 constant touch-ignore-bit
\ bit16 ~ bit31 : mode word
16 constant mode-word-start

\ 設定 line mode 中對應的 bit
: +mode-bit ( bit -- )
  1 swap lshift line-mode or!
;

\ 清除 line mode 中對應的 bit
: -mode-bit ( bit -- )
  1 swap lshift invert line-mode and!
;

\ 回傳 mode 中對應的 mode bit 是否為 1
: mode-bit-on? ( mode mode-bit -- flag )
  1 swap lshift and 0<>
;

\ 將 mode word 存入 line-mode 的最後兩個 byte
: mode-word! ( word -- )
  mode-word-start lshift line-mode @ $ffff and or line-mode !
;

\ 取得 mode 中的 mode word
: mode-word? ( mode -- mode-word )
  mode-word-start rshift
;

\ 回傳目前路徑上的 cut bit 是否為 1
: cutting? ( -- flag )
  1 group! next-path-mode@ cut-bit mode-bit-on?
;

\ 回傳目前路徑上的 touch ignore bit 是否為 1
: touch-ignore-line? ( -- flag )
  1 group! next-path-mode@ touch-ignore-bit mode-bit-on?
;

\ 回傳目前路徑上的 mode word 是否為 111
: g111? ( -- flag )
  1 group! next-path-mode@ mode-word? 111 =
;

: g-move ( -- )
  1 group!
  rapid-traverse-rate@ feedrate!
  line-mode @ path-mode!
  line-id @ path-id!
  px@ py@ pz@ line3d
  0 path-mode!
  0 path-id!
  0 line-mode !
  0 line-id !
;

: g00 ( -- )
  0 mode-word!
;

: g01 ( -- )
  cut-bit +mode-bit
  1 mode-word!
;

\ G04
: g04x ( F: r -- ) 1000.0e f* f>s ms  ;
    
: g04p ( n -- ) ." log|n * 0.0001 sec" cr
    drop ;

\ G17
: g17  ( -- ) ." log|g17" cr ;

\ G54P
: g54p ( n -- )
  wait-trj-end reset-trj
  1 group! wcs@ pcs3d  
  ;

\ G80
: g80 ( -- ) ." log|g80" cr ;
: g81z ( F: z -- ) ." log|g81z" fdrop cr ;

\ G90/91
: g90 ( -- ) ." log|g90" cr ;
: g91 ( -- ) ." log|g91" cr ;

\ G92
: g92 ( F: x y z -- )  
  wait-trj-end reset-trj
  pz! py! px!
  px@ py@ pz@ move3d
  ;
  
  
\ M Code

\ m00
: m00 
  wait-trj-end ." log|m00" cr
  1 suspend
  ;

\ m01
variable m01-option
: +m01 true m01-option ! ;
: -m01 false m01-option ! ;
: m01 
  wait-trj-end
  m01-option @ if ." log|m01" cr 1 suspend then 
  ;

\ m02
: m02 ." log|m02" cr ;

\ m25
: m25
  touch-ignore-bit +mode-bit
;

\ mcode
: mcode ( code -- )
  case
    0 of  m00  endof
    1 of  m01  endof
    2 of  m02  endof
    25 of m25  endof
    ." error|invalid mcode. ;A1540" dup  0 .r cr
  endcase
  ;


\ GPACK

\ G111
: g111 ( F: l -- )
  PECM-user-axis @ 
  case
      1 of  px!  endof
      2 of  py!  endof
      3 of  pz!  endof
  endcase 
  cut-bit +mode-bit
  111 mode-word!
  g-move
;

marker -work
