﻿// <auto-generated />
grammar csharp;

compilation_unit
  : extern_alias_directive* using_directive* attribute_list* member_declaration*
  ;

extern_alias_directive
  : 'extern' 'alias' identifier_token ';'
  ;

using_directive
  : 'global'? 'using' ('static' | name_equals)? name ';'
  ;

name_equals
  : identifier_name '='
  ;

identifier_name
  : 'global'
  | identifier_token
  ;

name
  : alias_qualified_name
  | qualified_name
  | simple_name
  ;

alias_qualified_name
  : identifier_name '::' simple_name
  ;

simple_name
  : generic_name
  | identifier_name
  ;

generic_name
  : identifier_token type_argument_list
  ;

type_argument_list
  : '<' (type (',' type)*)? '>'
  ;

qualified_name
  : name '.' simple_name
  ;

attribute_list
  : '[' attribute_target_specifier? attribute (',' attribute)* ']'
  ;

attribute_target_specifier
  : syntax_token ':'
  ;

attribute
  : name attribute_argument_list?
  ;

attribute_argument_list
  : '(' (attribute_argument (',' attribute_argument)*)? ')'
  ;

attribute_argument
  : (name_equals? | name_colon?) expression
  ;

name_colon
  : identifier_name ':'
  ;

member_declaration
  : base_field_declaration
  | base_method_declaration
  | base_property_declaration
  | base_type_declaration
  | delegate_declaration
  | enum_member_declaration
  | global_statement
  | incomplete_member
  | namespace_declaration
  ;

base_field_declaration
  : event_field_declaration
  | field_declaration
  ;

event_field_declaration
  : attribute_list* modifier* 'event' variable_declaration ';'
  ;

modifier
  : 'abstract'
  | 'async'
  | 'const'
  | 'extern'
  | 'fixed'
  | 'internal'
  | 'new'
  | 'override'
  | 'partial'
  | 'private'
  | 'protected'
  | 'public'
  | 'readonly'
  | 'ref'
  | 'sealed'
  | 'static'
  | 'unsafe'
  | 'virtual'
  | 'volatile'
  ;

variable_declaration
  : type variable_declarator (',' variable_declarator)*
  ;

variable_declarator
  : identifier_token bracketed_argument_list? equals_value_clause?
  ;

bracketed_argument_list
  : '[' argument (',' argument)* ']'
  ;

argument
  : name_colon? ('ref' | 'out' | 'in')? expression
  ;

equals_value_clause
  : '=' expression
  ;

field_declaration
  : attribute_list* modifier* variable_declaration ';'
  ;

base_method_declaration
  : constructor_declaration
  | conversion_operator_declaration
  | destructor_declaration
  | method_declaration
  | operator_declaration
  ;

constructor_declaration
  : attribute_list* modifier* identifier_token parameter_list constructor_initializer? (block | (arrow_expression_clause ';'))
  ;

parameter_list
  : '(' (parameter (',' parameter)*)? ')'
  ;

parameter
  : attribute_list* modifier* type? (identifier_token | '__arglist') equals_value_clause?
  ;

constructor_initializer
  : ':' ('base' | 'this') argument_list
  ;

argument_list
  : '(' (argument (',' argument)*)? ')'
  ;

block
  : attribute_list* '{' statement* '}'
  ;

arrow_expression_clause
  : '=>' expression
  ;

conversion_operator_declaration
  : attribute_list* modifier* ('implicit' | 'explicit') explicit_interface_specifier? 'operator' type parameter_list (block | (arrow_expression_clause ';'))
  ;

explicit_interface_specifier
  : name '.'
  ;

destructor_declaration
  : attribute_list* modifier* '~' identifier_token parameter_list (block | (arrow_expression_clause ';'))
  ;

method_declaration
  : attribute_list* modifier* type explicit_interface_specifier? identifier_token type_parameter_list? parameter_list type_parameter_constraint_clause* (block | (arrow_expression_clause ';'))
  ;

type_parameter_list
  : '<' type_parameter (',' type_parameter)* '>'
  ;

type_parameter
  : attribute_list* ('in' | 'out')? identifier_token
  ;

type_parameter_constraint_clause
  : 'where' identifier_name ':' type_parameter_constraint (',' type_parameter_constraint)*
  ;

type_parameter_constraint
  : class_or_struct_constraint
  | constructor_constraint
  | default_constraint
  | type_constraint
  ;

class_or_struct_constraint
  : 'class' '?'?
  | 'struct' '?'?
  ;

constructor_constraint
  : 'new' '(' ')'
  ;

default_constraint
  : 'default'
  ;

type_constraint
  : type
  ;

operator_declaration
  : attribute_list* modifier* type explicit_interface_specifier? 'operator' ('+' | '-' | '!' | '~' | '++' | '--' | '*' | '/' | '%' | '<<' | '>>' | '|' | '&' | '^' | '==' | '!=' | '<' | '<=' | '>' | '>=' | 'false' | 'true' | 'is') parameter_list (block | (arrow_expression_clause ';'))
  ;

base_property_declaration
  : event_declaration
  | indexer_declaration
  | property_declaration
  ;

event_declaration
  : attribute_list* modifier* 'event' type explicit_interface_specifier? identifier_token (accessor_list | ';')
  ;

accessor_list
  : '{' accessor_declaration* '}'
  ;

accessor_declaration
  : attribute_list* modifier* ('get' | 'set' | 'init' | 'add' | 'remove' | identifier_token) (block | (arrow_expression_clause ';'))
  ;

indexer_declaration
  : attribute_list* modifier* type explicit_interface_specifier? 'this' bracketed_parameter_list (accessor_list | (arrow_expression_clause ';'))
  ;

bracketed_parameter_list
  : '[' parameter (',' parameter)* ']'
  ;

property_declaration
  : attribute_list* modifier* type explicit_interface_specifier? identifier_token (accessor_list | ((arrow_expression_clause | equals_value_clause) ';'))
  ;

base_type_declaration
  : enum_declaration
  | type_declaration
  ;

enum_declaration
  : attribute_list* modifier* 'enum' identifier_token base_list? '{' (enum_member_declaration (',' enum_member_declaration)* ','?)? '}' ';'?
  ;

base_list
  : ':' base_type (',' base_type)*
  ;

base_type
  : primary_constructor_base_type
  | simple_base_type
  ;

primary_constructor_base_type
  : type argument_list
  ;

simple_base_type
  : type
  ;

enum_member_declaration
  : attribute_list* modifier* identifier_token equals_value_clause?
  ;

type_declaration
  : class_declaration
  | interface_declaration
  | record_declaration
  | struct_declaration
  ;

class_declaration
  : attribute_list* modifier* 'class' identifier_token type_parameter_list? base_list? type_parameter_constraint_clause* '{' member_declaration* '}' ';'?
  ;

interface_declaration
  : attribute_list* modifier* 'interface' identifier_token type_parameter_list? base_list? type_parameter_constraint_clause* '{' member_declaration* '}' ';'?
  ;

record_declaration
  : attribute_list* modifier* syntax_token ('class' | 'struct')? identifier_token type_parameter_list? parameter_list? base_list? type_parameter_constraint_clause* '{'? member_declaration* '}'? ';'?
  ;

struct_declaration
  : attribute_list* modifier* 'struct' identifier_token type_parameter_list? base_list? type_parameter_constraint_clause* '{' member_declaration* '}' ';'?
  ;

delegate_declaration
  : attribute_list* modifier* 'delegate' type identifier_token type_parameter_list? parameter_list type_parameter_constraint_clause* ';'
  ;

global_statement
  : attribute_list* modifier* statement
  ;

incomplete_member
  : attribute_list* modifier* type?
  ;

namespace_declaration
  : attribute_list* modifier* 'namespace' name '{' extern_alias_directive* using_directive* member_declaration* '}' ';'?
  ;

type
  : array_type
  | function_pointer_type
  | name
  | nullable_type
  | omitted_type_argument
  | pointer_type
  | predefined_type
  | ref_type
  | tuple_type
  ;

array_type
  : type array_rank_specifier+
  ;

array_rank_specifier
  : '[' (expression (',' expression)*)? ']'
  ;

function_pointer_type
  : 'delegate' '*' function_pointer_calling_convention? function_pointer_parameter_list
  ;

function_pointer_calling_convention
  : 'managed' function_pointer_unmanaged_calling_convention_list?
  | 'unmanaged' function_pointer_unmanaged_calling_convention_list?
  ;

function_pointer_unmanaged_calling_convention_list
  : '[' function_pointer_unmanaged_calling_convention (',' function_pointer_unmanaged_calling_convention)* ']'
  ;

function_pointer_unmanaged_calling_convention
  : identifier_token
  ;

function_pointer_parameter_list
  : '<' function_pointer_parameter (',' function_pointer_parameter)* '>'
  ;

function_pointer_parameter
  : attribute_list* modifier* type
  ;

nullable_type
  : type '?'
  ;

omitted_type_argument
  : /* epsilon */
  ;

pointer_type
  : type '*'
  ;

predefined_type
  : 'bool'
  | 'byte'
  | 'char'
  | 'decimal'
  | 'double'
  | 'float'
  | 'int'
  | 'long'
  | 'object'
  | 'sbyte'
  | 'short'
  | 'string'
  | 'uint'
  | 'ulong'
  | 'ushort'
  | 'void'
  ;

ref_type
  : 'ref' 'readonly'? type
  ;

tuple_type
  : '(' tuple_element (',' tuple_element)+ ')'
  ;

tuple_element
  : type identifier_token?
  ;

statement
  : block
  | break_statement
  | checked_statement
  | common_for_each_statement
  | continue_statement
  | do_statement
  | empty_statement
  | expression_statement
  | fixed_statement
  | for_statement
  | goto_statement
  | if_statement
  | labeled_statement
  | local_declaration_statement
  | local_function_statement
  | lock_statement
  | return_statement
  | switch_statement
  | throw_statement
  | try_statement
  | unsafe_statement
  | using_statement
  | while_statement
  | yield_statement
  ;

break_statement
  : attribute_list* 'break' ';'
  ;

checked_statement
  : attribute_list* ('checked' | 'unchecked') block
  ;

common_for_each_statement
  : for_each_statement
  | for_each_variable_statement
  ;

for_each_statement
  : attribute_list* 'await'? 'foreach' '(' type identifier_token 'in' expression ')' statement
  ;

for_each_variable_statement
  : attribute_list* 'await'? 'foreach' '(' expression 'in' expression ')' statement
  ;

continue_statement
  : attribute_list* 'continue' ';'
  ;

do_statement
  : attribute_list* 'do' statement 'while' '(' expression ')' ';'
  ;

empty_statement
  : attribute_list* ';'
  ;

expression_statement
  : attribute_list* expression ';'
  ;

fixed_statement
  : attribute_list* 'fixed' '(' variable_declaration ')' statement
  ;

for_statement
  : attribute_list* 'for' '(' (variable_declaration? | (expression (',' expression)*)?) ';' expression? ';' (expression (',' expression)*)? ')' statement
  ;

goto_statement
  : attribute_list* 'goto' ('case' | 'default')? expression? ';'
  ;

if_statement
  : attribute_list* 'if' '(' expression ')' statement else_clause?
  ;

else_clause
  : 'else' statement
  ;

labeled_statement
  : attribute_list* identifier_token ':' statement
  ;

local_declaration_statement
  : attribute_list* 'await'? 'using'? modifier* variable_declaration ';'
  ;

local_function_statement
  : attribute_list* modifier* type identifier_token type_parameter_list? parameter_list type_parameter_constraint_clause* (block | (arrow_expression_clause ';'))
  ;

lock_statement
  : attribute_list* 'lock' '(' expression ')' statement
  ;

return_statement
  : attribute_list* 'return' expression? ';'
  ;

switch_statement
  : attribute_list* 'switch' '('? expression ')'? '{' switch_section* '}'
  ;

switch_section
  : switch_label+ statement+
  ;

switch_label
  : case_pattern_switch_label
  | case_switch_label
  | default_switch_label
  ;

case_pattern_switch_label
  : 'case' pattern when_clause? ':'
  ;

pattern
  : binary_pattern
  | constant_pattern
  | declaration_pattern
  | discard_pattern
  | parenthesized_pattern
  | recursive_pattern
  | relational_pattern
  | type_pattern
  | unary_pattern
  | var_pattern
  ;

binary_pattern
  : pattern ('or' | 'and') pattern
  ;

constant_pattern
  : expression
  ;

declaration_pattern
  : type variable_designation
  ;

variable_designation
  : discard_designation
  | parenthesized_variable_designation
  | single_variable_designation
  ;

discard_designation
  : '_'
  ;

parenthesized_variable_designation
  : '(' (variable_designation (',' variable_designation)*)? ')'
  ;

single_variable_designation
  : identifier_token
  ;

discard_pattern
  : '_'
  ;

parenthesized_pattern
  : '(' pattern ')'
  ;

recursive_pattern
  : type? positional_pattern_clause? property_pattern_clause? variable_designation?
  ;

positional_pattern_clause
  : '(' (subpattern (',' subpattern)*)? ')'
  ;

subpattern
  : base_expression_colon? pattern
  ;

base_expression_colon
  : expression_colon
  | name_colon
  ;

expression_colon
  : expression ':'
  ;

property_pattern_clause
  : '{' (subpattern (',' subpattern)* ','?)? '}'
  ;

relational_pattern
  : '!=' expression
  | '<' expression
  | '<=' expression
  | '==' expression
  | '>' expression
  | '>=' expression
  ;

type_pattern
  : type
  ;

unary_pattern
  : 'not' pattern
  ;

var_pattern
  : 'var' variable_designation
  ;

when_clause
  : 'when' expression
  ;

case_switch_label
  : 'case' expression ':'
  ;

default_switch_label
  : 'default' ':'
  ;

throw_statement
  : attribute_list* 'throw' expression? ';'
  ;

try_statement
  : attribute_list* 'try' block catch_clause* finally_clause?
  ;

catch_clause
  : 'catch' catch_declaration? catch_filter_clause? block
  ;

catch_declaration
  : '(' type identifier_token? ')'
  ;

catch_filter_clause
  : 'when' '(' expression ')'
  ;

finally_clause
  : 'finally' block
  ;

unsafe_statement
  : attribute_list* 'unsafe' block
  ;

using_statement
  : attribute_list* 'await'? 'using' '(' (variable_declaration | expression) ')' statement
  ;

while_statement
  : attribute_list* 'while' '(' expression ')' statement
  ;

yield_statement
  : attribute_list* 'yield' ('return' | 'break') expression? ';'
  ;

expression
  : anonymous_function_expression
  | anonymous_object_creation_expression
  | array_creation_expression
  | assignment_expression
  | await_expression
  | base_object_creation_expression
  | binary_expression
  | cast_expression
  | checked_expression
  | conditional_access_expression
  | conditional_expression
  | declaration_expression
  | default_expression
  | element_access_expression
  | element_binding_expression
  | implicit_array_creation_expression
  | implicit_element_access
  | implicit_stack_alloc_array_creation_expression
  | initializer_expression
  | instance_expression
  | interpolated_string_expression
  | invocation_expression
  | is_pattern_expression
  | literal_expression
  | make_ref_expression
  | member_access_expression
  | member_binding_expression
  | omitted_array_size_expression
  | parenthesized_expression
  | postfix_unary_expression
  | prefix_unary_expression
  | query_expression
  | range_expression
  | ref_expression
  | ref_type_expression
  | ref_value_expression
  | size_of_expression
  | stack_alloc_array_creation_expression
  | switch_expression
  | throw_expression
  | tuple_expression
  | type
  | type_of_expression
  | with_expression
  ;

anonymous_function_expression
  : anonymous_method_expression
  | lambda_expression
  ;

anonymous_method_expression
  : modifier* 'delegate' parameter_list? block expression?
  ;

lambda_expression
  : parenthesized_lambda_expression
  | simple_lambda_expression
  ;

parenthesized_lambda_expression
  : attribute_list* modifier* parameter_list '=>' (block | expression)
  ;

simple_lambda_expression
  : attribute_list* modifier* parameter '=>' (block | expression)
  ;

anonymous_object_creation_expression
  : 'new' '{' (anonymous_object_member_declarator (',' anonymous_object_member_declarator)* ','?)? '}'
  ;

anonymous_object_member_declarator
  : name_equals? expression
  ;

array_creation_expression
  : 'new' array_type initializer_expression?
  ;

initializer_expression
  : '{' (expression (',' expression)* ','?)? '}'
  ;

assignment_expression
  : expression ('=' | '+=' | '-=' | '*=' | '/=' | '%=' | '&=' | '^=' | '|=' | '<<=' | '>>=' | '??=') expression
  ;

await_expression
  : 'await' expression
  ;

base_object_creation_expression
  : implicit_object_creation_expression
  | object_creation_expression
  ;

implicit_object_creation_expression
  : 'new' argument_list initializer_expression?
  ;

object_creation_expression
  : 'new' type argument_list? initializer_expression?
  ;

binary_expression
  : expression ('+' | '-' | '*' | '/' | '%' | '<<' | '>>' | '||' | '&&' | '|' | '&' | '^' | '==' | '!=' | '<' | '<=' | '>' | '>=' | 'is' | 'as' | '??') expression
  ;

cast_expression
  : '(' type ')' expression
  ;

checked_expression
  : 'checked' '(' expression ')'
  | 'unchecked' '(' expression ')'
  ;

conditional_access_expression
  : expression '?' expression
  ;

conditional_expression
  : expression '?' expression ':' expression
  ;

declaration_expression
  : type variable_designation
  ;

default_expression
  : 'default' '(' type ')'
  ;

element_access_expression
  : expression bracketed_argument_list
  ;

element_binding_expression
  : bracketed_argument_list
  ;

implicit_array_creation_expression
  : 'new' '[' ','* ']' initializer_expression
  ;

implicit_element_access
  : bracketed_argument_list
  ;

implicit_stack_alloc_array_creation_expression
  : 'stackalloc' '[' ']' initializer_expression
  ;

instance_expression
  : base_expression
  | this_expression
  ;

base_expression
  : 'base'
  ;

this_expression
  : 'this'
  ;

interpolated_string_expression
  : '$"' interpolated_string_content* '"'
  | '$@"' interpolated_string_content* '"'
  ;

interpolated_string_content
  : interpolated_string_text
  | interpolation
  ;

interpolated_string_text
  : interpolated_string_text_token
  ;

interpolation
  : '{' expression interpolation_alignment_clause? interpolation_format_clause? '}'
  ;

interpolation_alignment_clause
  : ',' expression
  ;

interpolation_format_clause
  : ':' interpolated_string_text_token
  ;

invocation_expression
  : expression argument_list
  ;

is_pattern_expression
  : expression 'is' pattern
  ;

literal_expression
  : '__arglist'
  | 'default'
  | 'false'
  | 'null'
  | 'true'
  | character_literal_token
  | numeric_literal_token
  | string_literal_token
  ;

make_ref_expression
  : '__makeref' '(' expression ')'
  ;

member_access_expression
  : expression ('.' | '->') simple_name
  ;

member_binding_expression
  : '.' simple_name
  ;

omitted_array_size_expression
  : /* epsilon */
  ;

parenthesized_expression
  : '(' expression ')'
  ;

postfix_unary_expression
  : expression ('++' | '--' | '!')
  ;

prefix_unary_expression
  : '!' expression
  | '&' expression
  | '*' expression
  | '+' expression
  | '++' expression
  | '-' expression
  | '--' expression
  | '^' expression
  | '~' expression
  ;

query_expression
  : from_clause query_body
  ;

from_clause
  : 'from' type? identifier_token 'in' expression
  ;

query_body
  : query_clause+ select_or_group_clause query_continuation?
  ;

query_clause
  : from_clause
  | join_clause
  | let_clause
  | order_by_clause
  | where_clause
  ;

join_clause
  : 'join' type? identifier_token 'in' expression 'on' expression 'equals' expression join_into_clause?
  ;

join_into_clause
  : 'into' identifier_token
  ;

let_clause
  : 'let' identifier_token '=' expression
  ;

order_by_clause
  : 'orderby' ordering (',' ordering)*
  ;

ordering
  : expression ('ascending' | 'descending')?
  ;

where_clause
  : 'where' expression
  ;

select_or_group_clause
  : group_clause
  | select_clause
  ;

group_clause
  : 'group' expression 'by' expression
  ;

select_clause
  : 'select' expression
  ;

query_continuation
  : 'into' identifier_token query_body
  ;

range_expression
  : expression? '..' expression?
  ;

ref_expression
  : 'ref' expression
  ;

ref_type_expression
  : '__reftype' '(' expression ')'
  ;

ref_value_expression
  : '__refvalue' '(' expression ',' type ')'
  ;

size_of_expression
  : 'sizeof' '(' type ')'
  ;

stack_alloc_array_creation_expression
  : 'stackalloc' type initializer_expression?
  ;

switch_expression
  : expression 'switch' '{' (switch_expression_arm (',' switch_expression_arm)* ','?)? '}'
  ;

switch_expression_arm
  : pattern when_clause? '=>' expression
  ;

throw_expression
  : 'throw' expression
  ;

tuple_expression
  : '(' argument (',' argument)+ ')'
  ;

type_of_expression
  : 'typeof' '(' type ')'
  ;

with_expression
  : expression 'with' initializer_expression
  ;

xml_node
  : xml_c_data_section
  | xml_comment
  | xml_element
  | xml_empty_element
  | xml_processing_instruction
  | xml_text
  ;

xml_c_data_section
  : '<![CDATA[' xml_text_literal_token* ']]>'
  ;

xml_comment
  : '<!--' xml_text_literal_token* '-->'
  ;

xml_element
  : xml_element_start_tag xml_node* xml_element_end_tag
  ;

xml_element_start_tag
  : '<' xml_name xml_attribute* '>'
  ;

xml_name
  : xml_prefix? identifier_token
  ;

xml_prefix
  : identifier_token ':'
  ;

xml_attribute
  : xml_cref_attribute
  | xml_name_attribute
  | xml_text_attribute
  ;

xml_cref_attribute
  : xml_name '=' ('\'' | '"') cref ('\'' | '"')
  ;

cref
  : member_cref
  | qualified_cref
  | type_cref
  ;

member_cref
  : conversion_operator_member_cref
  | indexer_member_cref
  | name_member_cref
  | operator_member_cref
  ;

conversion_operator_member_cref
  : 'explicit' 'operator' type cref_parameter_list?
  | 'implicit' 'operator' type cref_parameter_list?
  ;

cref_parameter_list
  : '(' (cref_parameter (',' cref_parameter)*)? ')'
  ;

cref_parameter
  : 'in'? type
  | 'out'? type
  | 'ref'? type
  ;

indexer_member_cref
  : 'this' cref_bracketed_parameter_list?
  ;

cref_bracketed_parameter_list
  : '[' cref_parameter (',' cref_parameter)* ']'
  ;

name_member_cref
  : type cref_parameter_list?
  ;

operator_member_cref
  : 'operator' ('+' | '-' | '!' | '~' | '++' | '--' | '*' | '/' | '%' | '<<' | '>>' | '|' | '&' | '^' | '==' | '!=' | '<' | '<=' | '>' | '>=' | 'false' | 'true') cref_parameter_list?
  ;

qualified_cref
  : type '.' member_cref
  ;

type_cref
  : type
  ;

xml_name_attribute
  : xml_name '=' ('\'' | '"') identifier_name ('\'' | '"')
  ;

xml_text_attribute
  : xml_name '=' ('\'' | '"') xml_text_literal_token* ('\'' | '"')
  ;

xml_element_end_tag
  : '</' xml_name '>'
  ;

xml_empty_element
  : '<' xml_name xml_attribute* '/>'
  ;

xml_processing_instruction
  : '<?' xml_name xml_text_literal_token* '?>'
  ;

xml_text
  : xml_text_literal_token*
  ;

structured_trivia
  : directive_trivia
  | documentation_comment_trivia
  | skipped_tokens_trivia
  ;

directive_trivia
  : bad_directive_trivia
  | branching_directive_trivia
  | define_directive_trivia
  | end_if_directive_trivia
  | end_region_directive_trivia
  | error_directive_trivia
  | line_directive_trivia
  | load_directive_trivia
  | nullable_directive_trivia
  | pragma_checksum_directive_trivia
  | pragma_warning_directive_trivia
  | reference_directive_trivia
  | region_directive_trivia
  | shebang_directive_trivia
  | undef_directive_trivia
  | warning_directive_trivia
  ;

bad_directive_trivia
  : '#' syntax_token
  ;

branching_directive_trivia
  : conditional_directive_trivia
  | else_directive_trivia
  ;

conditional_directive_trivia
  : elif_directive_trivia
  | if_directive_trivia
  ;

elif_directive_trivia
  : '#' 'elif' expression
  ;

if_directive_trivia
  : '#' 'if' expression
  ;

else_directive_trivia
  : '#' 'else'
  ;

define_directive_trivia
  : '#' 'define' identifier_token
  ;

end_if_directive_trivia
  : '#' 'endif'
  ;

end_region_directive_trivia
  : '#' 'endregion'
  ;

error_directive_trivia
  : '#' 'error'
  ;

line_directive_trivia
  : '#' 'line' (numeric_literal_token | 'default' | 'hidden') string_literal_token?
  ;

load_directive_trivia
  : '#' 'load' string_literal_token
  ;

nullable_directive_trivia
  : '#' 'nullable' ('enable' | 'disable' | 'restore') ('warnings' | 'annotations')?
  ;

pragma_checksum_directive_trivia
  : '#' 'pragma' 'checksum' string_literal_token string_literal_token string_literal_token
  ;

pragma_warning_directive_trivia
  : '#' 'pragma' 'warning' ('disable' | 'restore') (expression (',' expression)*)?
  ;

reference_directive_trivia
  : '#' 'r' string_literal_token
  ;

region_directive_trivia
  : '#' 'region'
  ;

shebang_directive_trivia
  : '#' '!'
  ;

undef_directive_trivia
  : '#' 'undef' identifier_token
  ;

warning_directive_trivia
  : '#' 'warning'
  ;

documentation_comment_trivia
  : xml_node*
  ;

skipped_tokens_trivia
  : syntax_token*
  ;

base_argument_list
  : argument_list
  | bracketed_argument_list
  ;

base_cref_parameter_list
  : cref_bracketed_parameter_list
  | cref_parameter_list
  ;

base_parameter_list
  : bracketed_parameter_list
  | parameter_list
  ;

base_parameter
  : function_pointer_parameter
  | parameter
  ;

character_literal_token
  : /* see lexical specification */
  ;

expression_or_pattern
  : expression
  | pattern
  ;

identifier_token
  : /* see lexical specification */
  ;

interpolated_string_text_token
  : /* see lexical specification */
  ;

numeric_literal_token
  : /* see lexical specification */
  ;

string_literal_token
  : /* see lexical specification */
  ;

syntax_token
  : /* see lexical specification */
  ;

xml_text_literal_token
  : /* see lexical specification */
  ;
