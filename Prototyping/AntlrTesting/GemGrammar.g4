grammar GemGrammar;

/*
Parser rules
*/

program
    : statementList
    ;

statementList
    : statement+
    ;

statement
    : gcode
	| block
	| comment
    | emptyStatement
    | expressionStatement
    | ifStatement
    ;

comment
	: MultiLineComment
	| SingleLineComment
	;

gcode
	: GCodeId gcodeParamExpr* eos
	;

gcodeParamExpr
	: GCodeParam DecimalLiteral
	| GCodeParam '(' Identifier ')'
	;

block
    : '{' statementList? '}'
    ;

emptyStatement
    : SemiColon
    ;

expressionStatement
    : {_input.Lt(1).Type != OpenBrace}? expressionSequence eos
    ;

ifStatement
    : If '(' expressionSequence ')' statement (Else statement)?
    ;

expressionSequence
    : singleExpression (',' singleExpression)*
    ;

singleExpression
    : Identifier '++'													# PostIncrementExpression
    | Identifier '--'													# PostDecreaseExpression
    | '+' singleExpression                                                   # UnaryPlusExpression
    | '-' singleExpression                                                   # UnaryMinusExpression
    | '!' singleExpression                                                   # NotExpression
    | singleExpression ('*' | '/' | '%') singleExpression                    # MultiplicativeExpression
    | singleExpression ('+' | '-') singleExpression                          # AdditiveExpression
    | singleExpression relationalOperator singleExpression					# RelationalExpression
    | singleExpression equalityOperator singleExpression						 # EqualityExpression
    | singleExpression '&&' singleExpression                                 # LogicalAndExpression
    | singleExpression '||' singleExpression                                 # LogicalOrExpression
    | singleExpression '?' singleExpression ':' singleExpression             # TernaryExpression
    | Identifier '=' singleExpression										# AssignmentExpression
    | singleExpression assignmentOperator singleExpression                   # AssignmentOperatorExpression
    | Identifier                                                             # IdentifierExpression
    | literal                                                                # LiteralExpression
    | '(' expressionSequence ')'                                             # ParenthesizedExpression
    ;

assignmentOperator
    : '*='
    | '/='
    | '%='
    | '+='
    | '-='
    ;

equalityOperator
	: Equals_
	| NotEquals
	;

relationalOperator
	: LessThan
	| MoreThan
	| LessThanEquals
	| GreaterThanEquals
	;

literal
    : NullLiteral
    | BooleanLiteral
    | DecimalLiteral
	| StringLiteral
    ;

identifierName
    : Identifier
    | reservedWord
    ;

reservedWord
    : keyword
    ;

keyword
    : If
	| Else
    | Import
    ;

eos
    : SemiColon
    | EOF
    | {_input.Lt(1).Type == LineTerminator}?
    | {_input.Lt(1).Type == CloseBrace}?
    ;


/*
Lexer rules
*/

/// Line Terminators
LineTerminator:                 [\r\n] -> channel(HIDDEN);

GCodeParam:						[A-Z];
GCodeId:						[GMT] [0-9]+;

OpenBracket:                    '[';
CloseBracket:                   ']';
OpenParen:                      '(';
CloseParen:                     ')';
OpenBrace:                      '{';
CloseBrace:                     '}';
SemiColon:                      ';';
Comma:                          ',';
Assign:                         '=';
QuestionMark:                   '?';
Colon:                          ':';
Ellipsis:                       '...';
Dot:                            '.';
PlusPlus:                       '++';
MinusMinus:                     '--';
Plus:                           '+';
Minus:                          '-';
Not:                            '!';
Multiply:                       '*';
Divide:                         '/';
Modulus:                        '%';
LessThan:                       '<';
MoreThan:                       '>';
LessThanEquals:                 '<=';
GreaterThanEquals:              '>=';
Equals_:                        '==';
NotEquals:                      '!=';
And:                            '&&';
Or:                             '||';
MultiplyAssign:                 '*=';
DivideAssign:                   '/=';
ModulusAssign:                  '%=';
PlusAssign:                     '+=';
MinusAssign:                    '-=';

/// Null Literals

NullLiteral:                    'null';

/// Boolean Literals

BooleanLiteral:                 'true'
              |                 'false';

/// Numeric Literals

DecimalLiteral:                 '-'? DecimalIntegerLiteral '.' [0-9]* ExponentPart?
              |                 '-'? '.' [0-9]+ ExponentPart?
              |                 '-'? DecimalIntegerLiteral ExponentPart?
              ;

/// Keywords

Else:                           'else';
If:                             'if';

/// Future Reserved Words

Import:                         'import';

/// Identifier Names and Identifiers

Identifier:                     IdentifierStart IdentifierPart*;

/// String Literals
StringLiteral:                  '"' DoubleStringCharacter* '"'
            |                   '\'' SingleStringCharacter* '\''
            ;

TemplateStringLiteral:          '`' ('\\`' | ~'`')* '`';

WhiteSpaces:                    [\t ]+ -> channel(HIDDEN);

/// Comments

MultiLineComment:               '/*' .*? '*/';
SingleLineComment:              '//' ~[\r\n]*;

// Fragment rules

fragment DoubleStringCharacter
    : ~["\\\r\n]
    | '\\' EscapeSequence
    | LineContinuation
    ;

fragment SingleStringCharacter
    : ~['\\\r\n]
    | '\\' EscapeSequence
    | LineContinuation
    ;

fragment EscapeSequence
    : CharacterEscapeSequence
    | '0' // no digit ahead! TODO
    ;

fragment CharacterEscapeSequence
    : SingleEscapeCharacter
    | NonEscapeCharacter
    ;

fragment SingleEscapeCharacter
    : ['"\\bfnrtv]
    ;

fragment NonEscapeCharacter
    : ~['"\\bfnrtv0-9xu\r\n]
    ;

fragment EscapeCharacter
    : SingleEscapeCharacter
    | [0-9]
    | [xu]
    ;

fragment LineContinuation
    : '\\' LineTerminatorSequence 
    ;

fragment LineTerminatorSequence
    : '\r?\n'
    | LineTerminator
    ;

fragment DecimalIntegerLiteral
    : '0'
    | [1-9] [0-9]*
    ;

fragment ExponentPart
    : [eE] [+-]? [0-9]+
    ;

fragment IdentifierStart
    : [$]
    ;

fragment IdentifierPart
    : [a-zA-Z_]
    | [0-9]
    ;

