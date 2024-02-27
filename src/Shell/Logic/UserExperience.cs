namespace Dotnet.Shell.Logic
{
    /// <summary>
    /// The User Experience to use when console rendering
    /// </summary>
    public enum UserExperience
    {
        /// <summary>
        /// The classic mode - similar to Bash
        /// </summary>
        Classic,
        /// <summary>
        /// Enhanced mode with improved history
        /// </summary>
        Enhanced,
        /// <summary>
        /// The TMux enhanced version which uses Tmux popup functionality
        /// </summary>
        TmuxEnhanced
    }
}
