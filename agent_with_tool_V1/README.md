Login with MFA for OpenAI calls: 

1) az login --scope https://cognitiveservices.azure.com//.default --use-device-code
2) Test with: az account get-access-token --resource https://cognitiveservices.azure.com/
3) This should work using DefaultAzureCredential()