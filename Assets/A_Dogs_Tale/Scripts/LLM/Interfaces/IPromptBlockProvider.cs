using System.Collections.Generic;

public interface IPromptBlockProvider
{
    void AddSystemBlocks(List<string> systemBlocks, LLMBuildContext context);
    void AddContextBlocks(List<string> contextBlocks, LLMBuildContext context);
}