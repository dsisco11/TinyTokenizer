namespace TinyTokenizer;

public sealed partial class TokenParser
{
    #region Token Reader

    /// <summary>
    /// Token reader with lookahead support.
    /// </summary>
    private sealed class SimpleTokenReader : IDisposable
    {
        private readonly IEnumerator<SimpleToken> _enumerator;
        private readonly List<SimpleToken> _buffer = new();
        private int _position;
        private bool _exhausted;

        public SimpleTokenReader(IEnumerator<SimpleToken> enumerator)
        {
            _enumerator = enumerator;
        }

        public bool TryPeek(out SimpleToken token)
        {
            return TryPeek(0, out token);
        }

        public bool TryPeek(int offset, out SimpleToken token)
        {
            var targetIndex = _position + offset;

            while (_buffer.Count <= targetIndex && !_exhausted)
            {
                if (_enumerator.MoveNext())
                {
                    _buffer.Add(_enumerator.Current);
                }
                else
                {
                    _exhausted = true;
                }
            }

            if (targetIndex < _buffer.Count)
            {
                token = _buffer[targetIndex];
                return true;
            }

            token = default;
            return false;
        }

        public void Advance()
        {
            _position++;
        }

        public void Dispose()
        {
            _enumerator.Dispose();
        }
    }

    #endregion
}
