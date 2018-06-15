lexer grammar FanucGCodeLexer;

/*********************************************************************************
 * FANUC GCode Lexer Rules - (c) 2018 Phil McCarthy, MIT license.
 *
 ********************************************************************************/

HASH: '#' ;

// Arithmetic and logic operation described p440 of the manual.
PLUS: '+' ;
MINUS: '-';
MULTIPLY: '*';
DIVIDE: '/';
MOD: 'MOD' ;

EQUALS: '=';

fragment DIGIT: [0-9] ;
DIGITS: DIGIT+ ;
DECIMAL: DIGIT+ ('.' DIGIT*)?  // negative decimals handled in parser rules
       | '.' DIGIT+;


// Support for System Variables (constant string beginning with '_'
// followed by up to seven uppercase letters, numerics or underscores),
// some of which are array-ish supporting "var [ expr ]" type syntax.
// see p383 of man
// SYSTEM_VAR: '#' '_' [A-Z0-9_]+; ??
// Also supports System constants (p384):
// [#_EMPTY] => #0 or #3100
// [#_PI] => #3101
// [#_E] => #3102
// #500 - #549 are common variables, and can also be given a name
// using the SETVN command. eg
// SETVN 510[HAPPY, MY_AWESOME_VAR];
// [#MY_AWESOME_VAR] => #511
// text can be anything allowed in prog except (,),',',EOB,EOR,:

// #3000 (alarm) can also be refd as [#_ALM] = 3000 (ALARM MSG);

SYSTEMVAR_CONST_OR_COMMONVAR_IDENTIFIER: {_input.La(-1) == '#'}? ~[0-9[\]] [A-Za-z0-9_]*; 
// actually more chars could be allowed for a common var - this is more restrictive
// than it needs to be. 

COMMA: ',';

OPEN_BRACKET: '[' ;
CLOSE_BRACKET: ']' ;

START_END_PROGRAM: '%' ;

fragment LT: 'LT' ;
fragment GT: 'GT' ;
fragment LE: 'LE' ;
fragment GE: 'GE' ;
fragment EQ: 'EQ' ;
fragment NE: 'NE' ;
RELATIONAL_OP : LT | GT | LE | GE | EQ | NE;

// logical operators
fragment OR: 'OR' ;
fragment XOR: 'XOR' ;
fragment AND: 'AND' ;
LOGICAL_OP : OR | XOR | AND;

// logic flow keywords
IF: 'IF' ;
THEN: 'THEN' ;
GOTO: 'GOTO' ;
WHILE: 'WHILE';
DO: 'DO';
END: 'END';

// builtin functions - p443 of manual says all but POW can be abbreviated to their first two letters.
fragment SIN: ('SIN'|'SI') ;
fragment COS: ('COS'|'CO') ;
fragment TAN: ('TAN'|'TA') ;
fragment ASIN: ('ASIN'|'AS') ;
fragment ACOS: ('ACOS'|'AC') ;
fragment ATAN: ('ATAN'|'ATN'|'AT') ;
fragment SQRT: ('SQRT'|'SQR'|'SQ') ;
fragment ABS: ('ABS'|'AB') ;
fragment BIN: ('BIN'|'BI') ;
fragment BCD: ('BCD'|'BC') ;
fragment ROUND: ('ROUND'|'RND'|'RO') ;
fragment FIX: ('FIX'|'FI') ;
fragment FUP: ('FUP'|'FU') ;
fragment LN: 'LN' ;
fragment EXP: ('EXP'|'EX') ;
fragment POW: 'POW' ;
fragment ADP: ('ADP'|'AD') ;
fragment PRM: ('PRM'|'PR') ;

BUILTIN_FUNCTION: SIN | COS | TAN | ASIN | ACOS | ATAN | SQRT | ABS | BIN | BCD | ROUND | FIX | FUP | LN | EXP | POW | ADP | PRM;

// tokens for built-in functions with special parsing rules
AX: 'AX';
AXNUM: 'AXNUM';
SETVN: 'SETVN';
BPRNT: 'BPRNT'; // external output commands p.486
DPRNT: 'DPRNT';
POPEN: 'POPEN';
PCLOS: 'PCLOS';

// see fanuc manual section 16.4 (p440) for grammar specification
// see p.269 for block configuration details
PROGRAM_NUMBER_PREFIX: 'O' ;

SEQUENCE_NUMBER_PREFIX: 'N' ;

GCODE_PREFIX: [A-MP-Z] ;

// For G65 etc, the allowed args have rules. These are probably best validated during semantic analysis:
// Addresses G, L, N, O, and P cannot be used in arguments (p457) note there are two arg formats; ii has ABCIJKIJKIJK etc instead
// eg [A-FH-KMQ-Z] ;

WS : ' ' -> skip;

// end of block. because new lines are block delimiters, this can't be hidden I think
NEWLINE : '\n';

CTRL_OUT: '(' -> pushMode(ControlIn);

UNRECOGNISED_TEXT: .+?;

mode ControlIn;

// all the chars allowed in gcode comments, according to the Fanuc manual p.2277
CTRL_OUT_TEXT: [ "#$&'*+,\-./0-9:;<=>?@A-Z[\]_a-z]+;

CTRL_IN: ')'  -> popMode;
