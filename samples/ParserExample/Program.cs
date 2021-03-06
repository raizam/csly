﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using csly.whileLang.compiler;
using csly.whileLang.interpreter;
using csly.whileLang.model;
using csly.whileLang.parser;
using expressionparser;
using GenericLexerWithCallbacks;
using jsonparser;
using jsonparser.JsonModel;
using ParserTests;
using simpleExpressionParser;
using sly.lexer;
using sly.lexer.fsm;
using sly.parser;
using sly.parser.generator;
using sly.parser.syntax;
using sly.buildresult;

namespace ParserExample
{
    public enum TokenType
    {
        [Lexeme("a")] a = 1,
        [Lexeme("b")] b = 2,
        [Lexeme("c")] c = 3,
        [Lexeme("z")] z = 26,
        [Lexeme("r")] r = 21,
        [Lexeme("[ \\t]+", true)] WS = 100,
        [Lexeme("[\\r\\n]+", true, true)] EOL = 101
    }


    internal class Program
    {
        [Production("R : A b c ")]
        [Production("R : Rec b c ")]
        public static object R(List<object> args)
        {
            var result = "R(";
            result += args[0] + ",";
            result += (args[1] as Token<TokenType>).Value + ",";
            result += (args[2] as Token<TokenType>).Value;
            result += ")";
            return result;
        }

        [Production("A : a ")]
        [Production("A : z ")]
        public static object A(List<object> args)
        {
            var result = "A(";
            result += (args[0] as Token<TokenType>).Value;
            result += ")";
            return result;
        }

        [Production("Rec : r Rec ")]
        [Production("Rec :  ")]
        public static object Rec(List<object> args)
        {
            if (args.Count == 2)
            {
                var r = "Rec(" + (args[0] as Token<TokenType>).Value + "," + args[1] + ")";
                return r;
                ;
            }

            return "_";
            ;
        }


        private static void TestFactorial()
        {
            var whileParser = new WhileParser();
            var builder = new ParserBuilder<WhileToken, WhileAST>();
            var Parser = builder.BuildParser(whileParser, ParserType.EBNF_LL_RECURSIVE_DESCENT, "statement");
            ;

            var program = @"
(
    r:=1;
    i:=1;
    while i < 11 do 
    (";
            program += "\nprint \"r=\".r;\n";
            program += "r := r * i;\n";
            program += "print \"r=\".r;\n";
            program += "print \"i=\".i;\n";
            program += "i := i + 1 \n);\n";
            program += "return r)\n";
            var result = Parser.Result.Parse(program);
            var interpreter = new Interpreter();
            var context = interpreter.Interprete(result.Result);

            var compiler = new WhileCompiler();
            var code = compiler.TranspileToCSharp(program);
            var f = compiler.CompileToFunction(program);
            ;
        }


        private static void testLexerBuilder()
        {
            var builder = new FSMLexerBuilder<JsonToken>();


            // conf
            builder.IgnoreWS()
                .WhiteSpace(' ')
                .WhiteSpace('\t')
                .IgnoreEOL();

            // start machine definition
            builder.Mark("start");


            // string literal
            builder.Transition('\"')
                .Mark("in_string")
                .ExceptTransitionTo(new[] {'\"', '\\'}, "in_string")
                .Transition('\\')
                .Mark("escape")
                .AnyTransitionTo(' ', "in_string")
                .Transition('\"')
                .End(JsonToken.STRING)
                .Mark("string_end")
                .CallBack(match =>
                {
                    string upperVAlue = match.Result.Value.ToString().ToUpper();
                    match.Result.SpanValue = new ReadOnlyMemory<char>(upperVAlue.ToCharArray());
                    return match;
                });

            // accolades
            builder.GoTo("start")
                .Transition('{')
                .End(JsonToken.ACCG);

            builder.GoTo("start")
                .Transition('}')
                .End(JsonToken.ACCD);

            // corchets
            builder.GoTo("start")
                .Transition('[')
                .End(JsonToken.CROG);

            builder.GoTo("start")
                .Transition(']')
                .End(JsonToken.CROD);

            // 2 points
            builder.GoTo("start")
                .Transition(':')
                .End(JsonToken.COLON);

            // comma
            builder.GoTo("start")
                .Transition(',')
                .End(JsonToken.COMMA);

            //numeric
            builder.GoTo("start")
                .RangeTransition('0', '9')
                .Mark("in_int")
                .RangeTransitionTo('0', '9', "in_int")
                .End(JsonToken.INT)
                .Transition('.')
                .Mark("start_double")
                .RangeTransition('0', '9')
                .Mark("in_double")
                .RangeTransitionTo('0', '9', "in_double")
                .End(JsonToken.DOUBLE);


            var code = "{\n\"d\" : 42.42 ,\n\"i\" : 42 ,\n\"s\" : \"quarante-deux\",\n\"s2\":\"a\\\"b\"\n}";
            //code = File.ReadAllText("test.json");
            var lex = builder.Fsm;
            var r = lex.Run(code, 0);
            var total = "";
            while (r.IsSuccess)
            {
                var msg = $"{r.Result.TokenID} : {r.Result.Value} @{r.Result.Position}";
                total += msg + "\n";
                Console.WriteLine(msg);
                r = lex.Run(code);
            }
        }


        private static void testGenericLexerWhile()
        {
            var sw = new Stopwatch();

            var source = @"
(
    r:=1;
    i:=1;
    while i < 11 DO 
    ( 
    r := r * i;
    PRINT r;
    print i;
    i := i + 1 )
)";


            sw.Reset();
            sw.Start();
            var wpg = new WhileParserGeneric();
            var wbuilderGen = new ParserBuilder<WhileTokenGeneric, WhileAST>();
            var buildResultgen = wbuilderGen.BuildParser(wpg, ParserType.EBNF_LL_RECURSIVE_DESCENT, "statement");
            var parserGen = buildResultgen.Result;
            var rGen = parserGen.Parse(source);
            sw.Stop();
            Console.WriteLine($"generic parser : {sw.ElapsedMilliseconds} ms");
            if (!rGen.IsError)
            {
                var interpreter = new Interpreter();
                var ctx = interpreter.Interprete(rGen.Result);
                ;
            }
            else
            {
                rGen.Errors.ForEach(e => Console.WriteLine(e.ToString()));
            }


            ;
        }

        private static void testGenericLexerJson()
        {
            var sw = new Stopwatch();

            var source = File.ReadAllText("test.json");

            var wp = new EbnfJsonParser();
            sw.Reset();
            sw.Start();
            var wbuilder = new ParserBuilder<JsonToken, JSon>();
            var buildResult = wbuilder.BuildParser(wp, ParserType.EBNF_LL_RECURSIVE_DESCENT, "root");
            var parser = buildResult.Result;
            var r = parser.Parse(source);
            sw.Stop();
            Console.WriteLine($"json regex parser : {sw.ElapsedMilliseconds} ms");
            if (r.IsError) r.Errors.ForEach(e => Console.WriteLine(e.ToString()));


            sw.Reset();
            sw.Start();
            wbuilder = new ParserBuilder<JsonToken, JSon>();
            buildResult = wbuilder.BuildParser(wp, ParserType.EBNF_LL_RECURSIVE_DESCENT, "root");
            parser = buildResult.Result;
            parser.Lexer = new JSONLexer();
            r = parser.Parse(source);
            Console.WriteLine($"json hard coded lexer : {sw.ElapsedMilliseconds} ms");
            sw.Stop();


            sw.Reset();
            sw.Start();
            var wpg = new EbnfJsonGenericParser();
            var wbuilderGen = new ParserBuilder<JsonTokenGeneric, JSon>();
            var buildResultgen = wbuilderGen.BuildParser(wpg, ParserType.EBNF_LL_RECURSIVE_DESCENT, "root");
            var parserGen = buildResultgen.Result;
            var rGen = parserGen.Parse(source);
            sw.Stop();
            Console.WriteLine($"json generic parser : {sw.ElapsedMilliseconds} ms");
            if (rGen.IsError) rGen.Errors.ForEach(e => Console.WriteLine(e.ToString()));


            ;
        }

        private static void testJSONLexer()
        {
            var builder = new ParserBuilder<JsonToken, JSon>();
            var parser = builder.BuildParser(new JSONParser(), ParserType.EBNF_LL_RECURSIVE_DESCENT, "root");

            var source = "{ \"k\" : 1;\"k2\" : 1.1;\"k3\" : null;\"k4\" : false}";
            //source = File.ReadAllText("test.json");
            var lexer = new JSONLexer();
            var sw = new Stopwatch();
            sw.Start();
            var tokens = lexer.Tokenize(source);
            sw.Stop();
            Console.WriteLine($"hard coded lexer {tokens.Count()} tokens in {sw.ElapsedMilliseconds}ms");
            var sw2 = new Stopwatch();
            var start = DateTime.Now.Millisecond;
            sw2.Start();
            tokens = parser.Result.Lexer.Tokenize(source).ToList();
            sw2.Stop();
            var end = DateTime.Now.Millisecond;
            Console.WriteLine($"old lexer {tokens.Count()} tokens in {sw2.ElapsedMilliseconds}ms / {end - start}ms");


            ;
        }


        private static void testErrors()
        {
            var jsonParser = new JSONParser();
            var builder = new ParserBuilder<JsonToken, JSon>();
            var parser = builder.BuildParser(jsonParser, ParserType.LL_RECURSIVE_DESCENT, "root").Result;


            var source = @"{
    'one': 1,
    'bug':{,}
}".Replace("'", "\"");
            var r = parser.Parse(source);

            var isError = r.IsError; // true
            var root = r.Result; // null;
            var errors = r.Errors; // !null & count > 0
            var error = errors[0] as UnexpectedTokenSyntaxError<JsonToken>; // 
            var token = error.UnexpectedToken.TokenID; // comma
            var line = error.Line; // 3
            var column = error.Column; // 12
        }

        private static void TestRuleParser()
        {
            Console.WriteLine("hum hum...");
            var parserInstance = new RuleParser<EbnfToken>();
            var builder = new ParserBuilder<EbnfToken, IClause<EbnfToken>>();
            var r = builder.BuildParser(parserInstance, ParserType.LL_RECURSIVE_DESCENT, "rule");
            
            var parser = r.Result;
            var rule = parser.Parse("a ( b ) ", "clauses");
            ;
        }


        public static BuildResult<Parser<ExpressionToken, int>> buildSimpleExpressionParserWithContext()
        {
            
           
                var StartingRule = $"{typeof(SimpleExpressionParserWithContext).Name}_expressions";
                var parserInstance = new SimpleExpressionParserWithContext();
                var builder = new ParserBuilder<ExpressionToken, int>();
                var Parser = builder.BuildParser(parserInstance, ParserType.LL_RECURSIVE_DESCENT, StartingRule);
                return Parser;
        }

        public static void TestContextualParser()
        {
            var buildResult = buildSimpleExpressionParserWithContext();
            if (buildResult.IsError)
            {
                buildResult.Errors.ForEach(e =>
                {
                    Console.WriteLine(e.Level + " - "+e.Message);
                });
                return;
            }
            var parser = buildResult.Result;
            var res = parser.ParseWithContext("2 + a", new Dictionary<string, int> {{"a", 2}});
            Console.WriteLine($"result : ok:>{res.IsOk}< value:>{res.Result}<");
        }

        public static void TestTokenCallBacks()
        {
            var res = LexerBuilder.BuildLexer(new BuildResult<ILexer<CallbackTokens>>());
            if (!res.IsError)
            {
                var lexer = res.Result as GenericLexer<CallbackTokens>;
                CallBacksBuilder.BuildCallbacks<CallbackTokens>(lexer);

                var tokens = lexer.Tokenize("aaa bbb").ToList();
                ;
                foreach (var token in tokens)
                {
                    Console.WriteLine($"{token.TokenID} - {token.Value}");
                }
            }
            
        }

        public static void test104()
        {
            EBNFTests tests = new EBNFTests();
            tests.TestGroupSyntaxOptionIsNone();
            
        }

        private static void Main(string[] args)
        {
            //TestContextualParser();
            //TestTokenCallBacks();
            test104();
        }
    }
}