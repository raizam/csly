using System.Collections.Generic;
using sly.parser.syntax;

namespace sly.parser
{
    public class SyntaxParseResult<IN> where IN : struct
    {
        public ISyntaxNode<IN> Root { get; set; }

        public bool IsError { get; set; }

        public List<UnexpectedTokenSyntaxError<IN>> Errors { get; set; } = new List<UnexpectedTokenSyntaxError<IN>>();

        public int EndingPosition { get; set; }

        public bool IsEnded { get; set; }
    }
}