using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;

namespace agent_with_tool_V0.services
{
    public class PrivateClient
    {
        private readonly string endpoint;
        private readonly string modelDeploymentName;
        private readonly ChatClient chatClient;

        public PrivateClient(string endpoint, string modelDeploymentName)
        {
            this.endpoint = endpoint;
            this.modelDeploymentName = modelDeploymentName;

            var credential = new DefaultAzureCredential();
            var azureClient = new AzureOpenAIClient(
                new Uri(endpoint),
                credential
                );

            this.chatClient = azureClient.GetChatClient(modelDeploymentName);

        }

        public async Task<string> GetResponseAsync(string prompt)
        {
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a helpful assistant."),
                new UserChatMessage(prompt)
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0.7f,
                MaxOutputTokenCount = 100
            };


            try
            {
                ChatCompletion completion = await chatClient.CompleteChatAsync(
                    messages,
                    options
                );
                return completion.Content[0].Text;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return string.Empty;
            }
        }
    }
}