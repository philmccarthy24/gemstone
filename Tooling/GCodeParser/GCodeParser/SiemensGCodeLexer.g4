lexer grammar SiemensGCodeLexer;

/******************************************************
* SIEMENS GCode tokens - note this is a small (but commonly used) subset
* of all the allowed tokens in the Siemens GCode dialect
*
******************************************************/

// Data Type keywords - p.12-67 of programming manual
fragment INT_TYPE:		'INT';
fragment REAL_TYPE:		'REAL';
fragment BOOL_TYPE:		'BOOL';
fragment STRING_TYPE:	'STRING';
fragment AXIS_TYPE:		'AXIS';
//CHAR_TYPE:		'CHAR'; // not supported at this time
//FRAME_TYPE:		'FRAME'; // not supported at this time
TYPE: INT_TYPE | REAL_TYPE | BOOL_TYPE | STRING_TYPE | AXIS_TYPE;

fragment DIGITS: [0-9]+;

REAL_VAL: DIGITS 'EX' [+-]? DIGITS				// real vlaue without a decimal point
		| [0-9]* '.' DIGITS ('EX' [+-]? DIGITS)?	// real value with at least one decimal digit after the decimal point
		| DIGITS '.' [0-9]* ('EX' [+-]? DIGITS)?;	// real value with at least one decimal digit before the decimal point
INTEGER_VAL: DIGITS;		// this is unsigned
BOOL_VAL: 'TRUE'|'FALSE';

/*
AX_ADDR : 'AX';
G_ADDR: 'G';
AXIS_IDENT: 'X' | 'Y' | 'Z' | 'A' | 'B' | 'C';
F_ADDR: 'F';
S_ADDR: 'S';
T_ADDR: 'T';
D_ADDR: 'D';
M_ADDR: 'M';
*/
// H_T ? Auxilliary function? p2-5

// GCode addresses
ADDRESS: [A-KMO-QS-Z]
	   | 'ACC' // Axial acceleration
	   | 'ADIS' // rounding clearance for path functions
	   | 'AX' // Axis value (variable axis programming) p2-10
	   | 'CHR' // chamfer the contour corner
	   | 'FA' // axial feed. Takes a param [axis] or [spindle]
	   | 'FDA' // Axial feed for handwheel override p2-10
	   | 'FL' // Axial feed limit p2-10
	   | 'IP' // interpolation parameter (variable axis programming)
	   | 'OVR' // path override
	   | 'OVRA' // Axial override
	   | 'PO' // polynomial coefficient
	   | 'POS' // position axis. Takes a param [Axis]
	   | 'POSA' // position axis across block boundary. Takes a param [Axis]
	   | 'SPOS' // spindle position. Optionally takes a param [n]
	   | 'SPOSA' // spindle position across block boundary. Optionally takes a param [n]
	   | 'RND' // round the contour corner
	   | 'RNDM' // round contour corner (modally)
	   | 'AR+' //opening angle
	   | 'AP' // polar angle p2-8
	   | 'CR' // circle radius
	   | 'RP' // polar radius
;	

R:	'R'; // arithmetic parameter. Also treated as an address. Probably want to keep the token separate for variable rules?

MEAS: 'MEAS';
SUPA: 'SUPA';
SPOS: 'SPOS';

N_WORD: 'N' DIGITS;
BLOCK_END: '\n';
L_WORD: 'L' DIGITS;

// core set of keywords
CASE:		'CASE';
OF:			'OF';
DEFAULT:	'DEFAULT';
DEF:		'DEF';
DELETE:		'DELETE';
WRITE:		'WRITE';
FOR:		'FOR';
TO:			'TO';
ENDFOR:		'ENDFOR';
GOTOB:		'GOTOB';
GOTOF:		'GOTOF';
IF:			'IF';
ELSE:		'ELSE';
ENDIF:		'ENDIF';
PROC:		'PROC';
SAVE:		'SAVE';
ENDPROC:	'ENDPROC';
EXTERN:		'EXTERN';
STOPRE:		'STOPRE';
GETT:		'GETT';
MSG:		'MSG';

// math functions - listed in p2-12
fragment SIN:		'SIN';
fragment COS:		'COS';
fragment TAN:		'TAN';
fragment ASIN:		'ASIN';
fragment ACOS:		'ACOS';
fragment ATAN2:		'ATAN2';
fragment SQRT:		'SQRT';
fragment ABS:		'ABS';
fragment POT:		'POT'; // 2nd power / square
fragment TRUNC:		'TRUNC';
fragment ROUND:		'ROUND';
fragment LN:		'LN';
fragment EXP:		'EXP';
//TR:			'TR';	// what is this? from Dirk's scanner. can't see it on the main list
BUILTIN_FUNCTION: SIN | COS | TAN | ASIN | ACOS | ATAN2 | SQRT | ABS | POT | TRUNC | ROUND | LN | EXP;

// arithmetic operators - listed in p2-12
PLUS:		'+' ;
MINUS:		'-' ;
MULTIPLY:	'*' ;
DIVIDE:		'/' ;
DIVIDE_2:	'DIV' ; // note p2-12: int / int = real.   int DIV int = int.
MODULUS:	'MOD' ;
// CHAIN: ':' ; // for frame variables apparently. what is this operator?
EQUALS:		'=' ;

// for strings
CONCAT_OP: '<<';

// relational operators
fragment LT: '<' ;
fragment GT: '>' ;
fragment LE: '<=' ;
fragment GE: '>=' ;
fragment EQ: '==' ;
fragment NE: '<>' ;
RELATIONAL_OP : LT | GT | LE | GE | EQ | NE;

// logical operators
fragment AND:	'AND';
fragment OR:	'OR';
fragment XOR:	'XOR';
fragment NOT:	'NOT';
LOGICAL_OP: AND | OR | XOR | NOT;

COMMA: ',';
COLON: ':'; 
PARENTHESIS_LEFT: '(';
PARENTHESIS_RIGHT: ')';
SQUAREDBRACKET_OPEN: '[';
SQUAREDBRACKET_CLOSE: ']';

// PROGRAMIDENTIFIER: [A-Za-z]{2} [A-Za-z0-9_]{0,33};    // can't really see where this is used
// PROGRAM_BEGIN     what should this be? Can't see in our examples..
// PROGRAM_END: '%'; what should this be? Can't see in our examples.

fragment LETTER_OR_UNDERSCORE: [A-Za-z_];
fragment LETTERS_OR_NUMBERS_OR_UNDERSCORES: [A-Za-z_0-9]+;
IDENTIFIER: LETTER_OR_UNDERSCORE LETTER_OR_UNDERSCORE LETTERS_OR_NUMBERS_OR_UNDERSCORES*;
SYSTEM_VARIABLE: '$' LETTERS_OR_NUMBERS_OR_UNDERSCORES;

WS : ' ' -> skip;

SEMICOLON: ';' -> pushMode(Comment);

QUOTE: ('"' | '\'') -> pushMode(String);

ILLEGAL: .+?;

mode String;

// TODO: possibly constrain allowed characters in string content
STRING_TEXT: ~["\n]+;

END_QUOTE: ('"' | '\'')   -> type(QUOTE), popMode;

mode Comment;

// TODO: possibly constrain allowed characters in comment content
COMMENT_TEXT: ~[\n]+;

END_COMMENT: '\n'   -> type(BLOCK_END), popMode;