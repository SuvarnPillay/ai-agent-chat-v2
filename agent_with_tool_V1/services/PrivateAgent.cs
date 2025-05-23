using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using Azure.AI.Projects; //prerelease

namespace agent_with_tool_V0.services
{

    public class PrivateAgent
    {
        private readonly AgentsClient agentsClient;
        private readonly string agentId;

        private readonly string threadId;

        public PrivateAgent(string connectionString, string agentId, string threadId)
        {
            var credential = new DefaultAzureCredential();
            this.agentsClient = new AgentsClient(connectionString, credential);
            this.agentId = agentId;
            this.threadId = threadId;
        }

        public async Task<string> GetResponseAsync(string prompt, string? threadId = null)
        {
            try
            {
                string useThreadId = threadId ?? this.threadId;
                await agentsClient.CreateMessageAsync(useThreadId, MessageRole.User, prompt);
                var runResponse = await agentsClient.CreateRunAsync(useThreadId, agentId);
                var run = runResponse.Value;
                do
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    runResponse = await agentsClient.GetRunAsync(useThreadId, run.Id);
                } while (runResponse.Value.Status == RunStatus.Queued || runResponse.Value.Status == RunStatus.InProgress);

                Response<PageableList<ThreadMessage>> messagesResponse = await agentsClient.GetMessagesAsync(useThreadId);
                IReadOnlyList<ThreadMessage> messages = messagesResponse.Value.Data;

                string response = string.Empty;
                // Display messages
                foreach (ThreadMessage threadMessage in messages)
                {
                    if (threadMessage.Role == MessageRole.Agent)
                    {
                        foreach (MessageContent contentItem in threadMessage.ContentItems)
                        {
                            if (contentItem is MessageTextContent textItem)
                            {
                                response += textItem.Text;
                            }
                        }
                        return response;
                    }
                }
                return "[Error]: No agent reply found for this prompt.";
            }
            catch (Exception ex)
            {
                return $"[Error]: {ex.Message}";
            }
        }

        public async Task<string> CreateThreadAsync()
        {
            try
            {
                var threadResponse = await agentsClient.CreateThreadAsync();
                return threadResponse.Value.Id;
            }
            catch (Exception ex)
            {
                // Log the error and return a message for debugging
                return $"[Error]: {ex.Message} | {ex.StackTrace}";
            }
        }
    }
}