﻿using System;
using System.Collections.Generic;
using AtlusScriptLib.Common.Text.OutputProviders;

namespace AtlusScriptLib.MessageScriptLanguage.Decompiler
{
    public class MessageScriptDecompiler : IDisposable
    {
        private ITextOutputProvider mOutput;
        private IMessageScriptFunctionResolver mResolver;

        public ITextOutputProvider TextOutputProvider
        {
            get => mOutput;
            set => mOutput = value;
        }

        public IMessageScriptFunctionResolver FunctionResolver
        {
            get => mResolver;
            set => mResolver = value;
        }

        public void Decompile( MessageScript script )
        {
            foreach ( var message in script.Windows )
            {
                Decompile( message );
                mOutput.WriteLine();
            }
        }

        public void Decompile( IMessageScriptWindow message )
        {
            switch ( message.Type )
            {
                case MessageScriptWindowType.Dialogue:
                    Decompile( ( MessageScriptDialogWindow )message );
                    break;
                case MessageScriptWindowType.Selection:
                    Decompile( ( MessageScriptSelectionWindow )message );
                    break;

                default:
                    throw new NotImplementedException( message.Type.ToString() );
            }
        }

        public void Decompile( MessageScriptDialogWindow message )
        {
            if ( message.Speaker != null )
            {
                switch ( message.Speaker.Type )
                {
                    case MessageScriptSpeakerType.Named:
                        {
                            WriteOpenTag( "dlg" );
                            WriteTagArgument( message.Identifier );
                            {
                                mOutput.Write( " " );

                                var speaker = ( MessageScriptNamedSpeaker )message.Speaker;
                                if ( speaker.Name != null )
                                {
                                    WriteOpenTag();
                                    Decompile( speaker.Name, false );
                                    WriteCloseTag();
                                }
                            }
                            WriteCloseTag();
                        }
                        break;

                    case MessageScriptSpeakerType.Variable:
                        {
                            WriteOpenTag( "dlg" );
                            WriteTagArgument( message.Identifier );
                            {
                                mOutput.Write( " " );
                                WriteOpenTag();
                                mOutput.Write( ( ( MessageScriptVariableSpeaker )message.Speaker ).Index.ToString() );
                                WriteCloseTag();
                            }
                            WriteCloseTag();
                        }
                        break;
                }
            }
            else
            {
                WriteTag( "dlg", message.Identifier );
            }

            mOutput.WriteLine();

            foreach ( var line in message.Lines )
            {
                Decompile( line );
                mOutput.WriteLine();
            }
        }

        public void Decompile( MessageScriptSelectionWindow message )
        {
            WriteTag( "sel", message.Identifier );
            mOutput.WriteLine();

            foreach ( var line in message.Lines )
            {
                Decompile( line );
                mOutput.WriteLine();
            }
        }

        public void Decompile( MessageScriptLine line, bool emitLineEndTag = true )
        {
            foreach ( var token in line.Tokens )
            {
                Decompile( token );
            }

            if ( emitLineEndTag )
                WriteTag( "e" );
        }

        public void Decompile( IMessageScriptLineToken token )
        {
            switch ( token.Type )
            {
                case MessageScriptTokenType.Text:
                    Decompile( ( MessageScriptTextToken )token );
                    break;
                case MessageScriptTokenType.Function:
                    Decompile( ( MessageScriptFunctionToken )token );
                    break;
                case MessageScriptTokenType.CodePoint:
                    Decompile( ( MessageScriptCodePointToken )token );
                    break;
                case MessageScriptTokenType.NewLine:
                    Decompile( ( MessageScriptNewLineToken )token );
                    break;

                default:
                    throw new NotImplementedException( token.Type.ToString() );
            }
        }

        public void Decompile( MessageScriptFunctionToken token )
        {
            if ( mResolver != null &&
                mResolver.TryResolveFunction( token.FunctionTableIndex, token.FunctionIndex, out MessageScriptFunctionDefinition definition ) )
            {
                WriteOpenTag( definition.Tag );

                foreach ( var argument in definition.Arguments )
                {
                    WriteTagArgument( token.Arguments[argument.OriginalArgumentIndex].ToString() );
                }

                WriteCloseTag();
            }
            else
            {
                if ( token.Arguments.Count == 0 )
                {
                    WriteTag( "f", token.FunctionTableIndex.ToString(), token.FunctionIndex.ToString() );
                }
                else
                {
                    WriteOpenTag( "f" );
                    WriteTagArgument( token.FunctionTableIndex.ToString() );
                    WriteTagArgument( token.FunctionIndex.ToString() );

                    foreach ( var tokenArgument in token.Arguments )
                    {
                        WriteTagArgument( tokenArgument.ToString() );
                    }

                    WriteCloseTag();
                }
            }
        }

        public void Decompile( MessageScriptTextToken token )
        {
            mOutput.Write( token.Text );
        }

        public void Decompile( MessageScriptCodePointToken token )
        {
            WriteTag( $"x 0x{token.HighSurrogate:X2} 0x{token.LowSurrogate:X2}" );
        }

        public void Decompile( MessageScriptNewLineToken token )
        {
            WriteTag( "n" );
        }

        public void Dispose()
        {
            mOutput.Dispose();
        }

        private void WriteOpenTag()
        {
            mOutput.Write( "[" );
        }

        private void WriteOpenTag( string tag )
        {
            mOutput.Write( $"[{tag}" );
        }

        private void WriteTagArgument( string argument )
        {
            mOutput.Write( " " );
            mOutput.Write( argument );
        }

        private void WriteTagArgument( MessageScriptLine line )
        {
            mOutput.Write( " " );
            Decompile( line, false );
        }

        private void WriteTagArgumentTag( string tag, params string[] arguments )
        {
            mOutput.Write( " " );
            WriteTag( tag, arguments );
        }

        private void WriteCloseTag()
        {
            mOutput.Write( "]" );
        }

        private void WriteTag( string tag, params string[] arguments )
        {
            WriteOpenTag( tag );

            if ( arguments.Length != 0 )
            {
                foreach ( var argument in arguments )
                {
                    WriteTagArgument( argument );
                }
            }

            WriteCloseTag();
        }
    }

    public class MessageScriptFunctionDefinition
    {
        public string Tag { get; }

        public List<MessageScriptFunctionArgument> Arguments { get; }

        public MessageScriptFunctionDefinition( string tag )
        {
            Tag = tag ?? throw new ArgumentNullException( nameof( tag ) );
            Arguments = new List<MessageScriptFunctionArgument>();
        }

        public MessageScriptFunctionDefinition( string tag, List<MessageScriptFunctionArgument> arguments )
        {
            Tag = tag ?? throw new ArgumentNullException( nameof( tag ) );
            Arguments = arguments;
        }
    }

    public class MessageScriptFunctionArgument
    {
        public int OriginalArgumentIndex { get; }

        public MessageScriptFunctionArgument( int originalArgumentIndex )
        {
            OriginalArgumentIndex = originalArgumentIndex;
        }
    }

    public interface IMessageScriptFunctionResolver
    {
        bool TryResolveFunction( int tokenFunctionTableIndex, int tokenFunctionIndex, out MessageScriptFunctionDefinition messageScriptFunctionDefinition );
    }
}