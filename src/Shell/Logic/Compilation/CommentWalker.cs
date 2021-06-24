using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;

namespace Dotnet.Shell.Logic.Compilation
{
    internal class CommentWalker : CSharpSyntaxWalker
    {
        public List<Location> Comments = new List<Location>();
        public List<string> SingleLineComments = new List<string>();

        public CommentWalker(SyntaxWalkerDepth depth = SyntaxWalkerDepth.Trivia) : base(depth)
        {
        }

        public override void VisitTrivia(SyntaxTrivia trivia)
        {
            if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.XmlComment)
                || trivia.IsKind(SyntaxKind.XmlComment)
                || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
            {
                if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
                {
                    // we need to check to ensure that this isn't a URL masquerading as a comment.
                    // In .nsh files a single line comment can be 'escaped' by a :
                    var preText = trivia.SyntaxTree.GetText();
                    var startOfPossibleComment = preText.ToString().Substring(0, trivia.SpanStart);

                    if (trivia.Token.ValueText == ":" || startOfPossibleComment.EndsWith(":"))
                    {
                        return;
                    }

                    SingleLineComments.Add(trivia.ToFullString());
                }

                Comments.Add(trivia.GetLocation());
            }
        }
    }
}
