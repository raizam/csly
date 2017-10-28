﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sly.parser.syntax
{
    public interface ISyntaxNode<IN>
    {
        bool IsTerminal();

        string Dump(string tab);

        ISyntaxNode<IN> Clone();
    }
}
