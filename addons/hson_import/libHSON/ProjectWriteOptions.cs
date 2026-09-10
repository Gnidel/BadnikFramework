namespace libHSON
{
    public struct ProjectWriteOptions
    {
        #region Private Constants
        private const int IncludeUnnecessaryPropertiesBit = 1;
        #endregion Private Constants

        #region Private Fields
        private int _optionsMask;
        #endregion Private Fields

        #region Public Properties
        public bool IncludeUnnecessaryProperties
        {
            get
            {
                return (_optionsMask & IncludeUnnecessaryPropertiesBit) != 0;
            }
            set
            {
                if (value)
                {
                    _optionsMask |= IncludeUnnecessaryPropertiesBit;
                }
                else
                {
                    _optionsMask &= ~IncludeUnnecessaryPropertiesBit;
                }
            }
        }
        #endregion Public Properties
    }
}
