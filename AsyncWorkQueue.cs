using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Grammophone.Domos.Logic
{
	/// <summary>
	/// Queue for executing asynchronous message consumers. This is a thread-safe class.
	/// </summary>
	/// <typeparam name="M">The type of message to consume.</typeparam>
	public class AsyncWorkQueue<M> : Loggable
	{
		#region Private fields

		private ConcurrentQueue<M> messagesQueue;

		private int workerStartedFlag;

		private Func<M, Task> asyncMessageConsumer;

		private bool isShuttingDown;

		#endregion

		#region Construction

		/// <summary>
		/// Create.
		/// </summary>
		/// <param name="asyncMessageConsumer">Asynchronous functino for consuming a message.</param>
		public AsyncWorkQueue(Func<M, Task> asyncMessageConsumer)
		{
			if (asyncMessageConsumer == null) throw new ArgumentNullException(nameof(asyncMessageConsumer));

			this.asyncMessageConsumer = asyncMessageConsumer;

			messagesQueue = new ConcurrentQueue<M>();
		}

		#endregion

		#region Public methods

		/// <summary>
		/// Enqueue a message to be consumed.
		/// This is a thread-safe method.
		/// </summary>
		/// <param name="message">The message to consume.</param>
		public void Enqueue(M message)
		{
			if (isShuttingDown)
				throw new LogicException("The queue is shutting down; no new messages are accepted.");

			messagesQueue.Enqueue(message);

			EnsureWorkerTaskStarted();
		}

		/// <summary>
		/// Mark that the queue is not accepting any new messages.
		/// This is a thread-safe method.
		/// </summary>
		/// <returns>
		/// Returns a task whose completion marks that the queue is empty.
		/// </returns>
		public async Task ShutDownAsync()
		{
			isShuttingDown = true;

			await StartDequeAsync();
		}

		#endregion

		#region Private methods

		private void EnsureWorkerTaskStarted()
		{
			int wasWorkerStartedFlag = Interlocked.CompareExchange(ref workerStartedFlag, 1, 0);

			if (wasWorkerStartedFlag == 0)
			{
				var workertask = Task.Factory.StartNew(() =>
				{
					var dequeTask = StartDequeAsync();
				});
			}
		}

		private async Task StartDequeAsync()
		{
			await ServeMessagesAsync();

			workerStartedFlag = 0;

			// For race conditions: Did anyone make it to add work items before resetting the flag?
			// If yes, drain the rest of the messages again.
			// It doesn't matter if an additional worker task starts after this point; this situation is temporayy
			// because this worker task will die soon.

			await ServeMessagesAsync();
		}

		private async Task ServeMessagesAsync()
		{
			while (messagesQueue.TryDequeue(out var message))
			{
				try
				{
					await asyncMessageConsumer(message);
				}
				catch (Exception ex)
				{
					Logger.Error(ex, "Failed queued work.");
				}
			}
		}

		#endregion
	}
}
