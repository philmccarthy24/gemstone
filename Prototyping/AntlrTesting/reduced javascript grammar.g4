grammar GemGrammar;

/*
Parser definition
*/

program
    : statementList
    ;

statement
    : block
    | emptyStatement
    | expressionStatement
    | ifStatement
    ;

block
    : '{' statementList? '}'
    ;

statementList
    : statement+
    ;

emptyStatement
    : SemiColon
    ;

expressionStatement
    : {notOpenBraceAndNotFunction()}? expressionSequence eos
    ;

ifStatement
    : If '(' expressionSequence ')' statement (Else statement)?
    ;

expressionSequence
    : singleExpression (',' singleExpression)*
    ;

singleExpression
    : singleExpression {notLineTerminator()}? '++'                           # PostIncrementExpression
    | singleExpression {notLineTerminator()}? '--'                           # PostDecreaseExpression
    | '++' singleExpression                                                  # PreIncrementExpression
    | '--' singleExpression                                                  # PreDecreaseExpression
    | '+' singleExpression                                                   # UnaryPlusExpression
    | '-' singleExpression                                                   # UnaryMinusExpression
    | '!' singleExpression                                                   # NotExpression
    | singleExpression ('*' | '/' | '%') singleExpression                    # MultiplicativeExpression
    | singleExpression ('+' | '-') singleExpression                          # AdditiveExpression
    | singleExpression ('<' | '>' | '<=' | '>=') singleExpression            # RelationalExpression
    | singleExpression ('==' | '!=') singleExpression						 # EqualityExpression
    | singleExpression '&&' singleExpression                                 # LogicalAndExpression
    | singleExpression '||' singleExpression                                 # LogicalOrExpression
    | singleExpression '?' singleExpression ':' singleExpression             # TernaryExpression
    | singleExpression '=' singleExpression                                  # AssignmentExpression
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
    : Else
    | Var
    | If
    | Const
    | Import
    ;

eos
    : SemiColon
    | EOF
    | {lineTerminatorAhead()}?
    | {closeBrace()}?
    ;


/*
Lexer definitions 
*/

/// Line Terminators
LineTerminator:                 [\r\n] -> channel(HIDDEN);

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

DecimalLiteral:                 DecimalIntegerLiteral '.' [0-9]* ExponentPart?
              |                 '.' [0-9]+ ExponentPart?
              |                 DecimalIntegerLiteral ExponentPart?
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

MultiLineComment:               '/*' .*? '*/' -> channel(HIDDEN);
SingleLineComment:              '//' ~[\r\n]* -> channel(HIDDEN);
UnexpectedCharacter:            . ;

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
    : '\r\n'
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
    : [a-zA-Z]
    | [$_]
    ;

fragment IdentifierPart
    : IdentifierStart
    | [0-9]
    ;

