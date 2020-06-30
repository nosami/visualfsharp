// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor
open Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion
open Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data

type FSharpAsyncCompletionCommitManager() =
    interface IAsyncCompletionCommitManager with

        /// <summary>
        /// <para>
        /// Returns characters that may commit completion.
        /// </para>
        /// <para>
        /// When completion is active and a text edit matches one of these characters,
        /// <see cref="ShouldCommitCompletion(IAsyncCompletionSession, SnapshotPoint, char, CancellationToken)"/> is called to verify that the character
        /// is indeed a commit character at a given location.
        /// </para>
        /// <para>
        /// Called on UI thread.
        /// </para>
        /// </summary>
        override __.PotentialCommitCharacters = [' '; '='; ','; '.'; '<'; '>'; '('; ')'; '!'; ':'; '['; ']'; '|'] |> Seq.ofList

        /// <summary>
        /// <para>
        /// Returns whether <paramref name="typedChar"/> is a commit character at a given <paramref name="location"/>.
        /// </para>
        /// <para>
        /// If in your language every character returned by <see cref="PotentialCommitCharacters"/>
        /// is a commit character, simply return <see langword="true"/>.
        /// </para>
        /// <para>
        /// Called on UI thread.
        /// </para>
        /// </summary>
        /// <param name="session">The active <see cref="IAsyncCompletionSession"/></param>
        /// <param name="location">Location in the snapshot of the view's topmost buffer. The character is not inserted into this snapshot</param>
        /// <param name="typedChar">Character typed by the user</param>
        /// <param name="token">Token used to cancel this operation</param>
        /// <returns>True if this character should commit the active session</returns>
        override __.ShouldCommitCompletion(session, location, typedChar, token) = true

        /// <summary>
        /// <para>
        /// Allows the implementer of <see cref="IAsyncCompletionCommitManager"/> to customize how specified <see cref="CompletionItem"/> is committed.
        /// This method is called on UI thread, before the <paramref name="typedChar"/> is inserted into the buffer.
        /// </para>
        /// <para>
        /// In most cases, implementer does not need to commit the item. Return <see cref="CommitResult.Unhandled"/> to allow another
        /// <see cref="IAsyncCompletionCommitManager"/> to attempt the commit, or to invoke the default commit behavior.
        /// </para>
        /// <para>
        /// To perform a custom commit, replace contents of <paramref name="buffer"/>
        /// at a location indicated by <see cref="IAsyncCompletionSession.ApplicableToSpan"/>
        /// with text stored in <see cref="CompletionItem.InsertText"/>.
        /// To move the caret, use <see cref= "IAsyncCompletionSession.TextView" />.
        /// Finally, return <see cref="CommitResult.Handled"/>. Use <see cref="CommitResult.Behavior"/> to influence Editor's behavior
        /// after invoking this method.
        /// </para>
        /// <para>
        /// Called on UI thread.
        /// </para>
        /// </summary>
        /// <param name="session">The active <see cref="IAsyncCompletionSession"/>. See <see cref="IAsyncCompletionSession.ApplicableToSpan"/> and <see cref="IAsyncCompletionSession.TextView"/></param>
        /// <param name="buffer">Subject buffer which matches this <see cref="IAsyncCompletionCommitManager"/>'s content type</param>
        /// <param name="item">Which <see cref="CompletionItem"/> is to be committed</param>
        /// <param name="typedChar">Text change associated with this commit</param>
        /// <param name="token">Token used to cancel this operation</param>
        /// <returns>Instruction for the editor how to proceed after invoking this method. Default is <see cref="CommitResult.Unhandled"/></returns>
        override __.TryCommit(session, buffer, item, typedChar, token) = CommitResult.Unhandled
