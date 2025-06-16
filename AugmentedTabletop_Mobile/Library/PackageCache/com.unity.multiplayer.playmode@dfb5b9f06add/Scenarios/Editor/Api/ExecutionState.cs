namespace Unity.Multiplayer.PlayMode.Scenarios.Editor.Api
{
    // TODO MTT-10157 Update Execution State to remove Active, Aborted and remove redundant states
    // when integrating the new Execution Node system.

    /// <summary>
    /// Represents the state of an execution in a graph
    /// </summary>
    enum ExecutionState
    {
        /// <summary>
        /// Execution has failed and won't do anything until the next cycle. Will transition to Idle when the next cycle starts
        /// </summary>
        Failed,
        /// <summary>
        /// Initial state; Will transition to Running or Error when the Node
        /// </summary>
        Idle,
        /// <summary>
        /// Execution is running; building, uploading something, starting a process, etc. Will transition to Completed or Error when done
        /// </summary>
        Running,
        /// <summary>
        /// Execution is active and waiting for the process to finish. Will transition to Completed or Error when done
        /// </summary>
        Active,
        /// <summary>
        /// Execution has completed its tasks successfully and won't do anything until the next cycle. Will transition to Idle when the next cycle starts
        /// </summary>
        Completed,
        /// <summary>
        /// Execution has been aborted and won't do anything until the next cycle. Will transition to Idle when the next cycle starts
        /// </summary>
        Aborted,
        /// <summary>
        /// Execution is invalid and won't do anything until the next cycle. Will transition to Idle when the next cycle starts
        /// </summary>
        Invalid = int.MaxValue
    }
}
