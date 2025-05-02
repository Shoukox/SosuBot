namespace SosuBot.Localization
{
    public interface ILocalization
    {
        public string command_start { get; }
        public string command_help { get; }
        public string command_last { get; }
        public string command_set { get; }
        public string command_setMode { get; }
        public string command_score { get; }
        public string command_user { get; }
        public string command_compare { get; }
        public string command_userbest { get; }
        public string command_chatstats_title { get; }
        public string command_chatstats_row { get; }
        public string command_chatstats_end { get; }
        public string command_excluded { get; }
        public string command_included { get; }
        public string settings { get; }
        public string settings_language_ru { get; }
        public string settings_language_en { get; }
        public string settings_language_changedSuccessfully { get; }
        public string send_mapInfo { get; }
        public string waiting { get; }

        public string error_baseMessage { get; }
        public string error_userNotSetHimself { get; }
        public string error_hintReplaceSpaces { get; }
        public string error_nameIsEmpty { get; }
        public string error_modeIsEmpty { get; }
        public string error_modeIncorrect { get; }
        public string error_userNotFound { get; }
        public string error_specificUserNotFound { get; }
        public string error_userNotFoundInBotsDatabase { get; }
        public string error_noRecords { get; }
        public string error_argsLength { get; }
        public string error_noPreviousScores { get; }
        public string error_noBestScores { get; }
        public string error_excludeListAlreadyContainsThisId { get; }
        public string error_userWasNotExcluded { get; }
    }
}
