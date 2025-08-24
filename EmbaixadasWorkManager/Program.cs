using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SQS;
using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Services;
using Microsoft.Extensions.Options;

namespace EmbaixadasWorkManager;

public class Program
{
    public static void Main(string[] args)
    {
        IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Configurações
                services.Configure<AwsConfiguration>(
                    context.Configuration.GetSection(AwsConfiguration.SectionName));
                services.Configure<SqsConfiguration>(
                    context.Configuration.GetSection(SqsConfiguration.SectionName));
                services.Configure<DynamoDbConfiguration>(
                    context.Configuration.GetSection(DynamoDbConfiguration.SectionName));

                // Registrar configurações como singletons para injeção direta
                services.AddSingleton<SqsConfiguration>(provider =>
                {
                    var config = context.Configuration.GetSection(SqsConfiguration.SectionName).Get<SqsConfiguration>();
                    if (config == null)
                    {
                        throw new InvalidOperationException("Configuração SQS não encontrada");
                    }
                    return config;
                });

                // Cliente DynamoDB
                if (context.Configuration.GetValue<bool>("AWS:UseProfile"))
                {
                    // Usar perfil AWS CLI
                    services.AddAWSService<IAmazonDynamoDB>();
                }
                else
                {
                    // Usar credenciais do appsettings.json
                    services.AddSingleton<IAmazonDynamoDB>(provider =>
                    {
                        var awsConfig = context.Configuration.GetSection("AWS").Get<AwsConfiguration>();
                        if (awsConfig == null)
                        {
                            throw new InvalidOperationException("Configuração AWS não encontrada");
                        }
                        
                        var config = new AmazonDynamoDBConfig
                        {
                            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsConfig.Region)
                        };
                        
                        return new AmazonDynamoDBClient(awsConfig.AccessKey, awsConfig.SecretKey, config);
                    });
                }
                
                services.AddSingleton<IDynamoDBContext>(provider =>
                {
                    var client = provider.GetRequiredService<IAmazonDynamoDB>();
                    return new DynamoDBContext(client);
                });

                // Cliente SQS
                if (context.Configuration.GetValue<bool>("AWS:UseProfile"))
                {
                    // Usar perfil AWS CLI
                    services.AddAWSService<IAmazonSQS>();
                }
                else
                {
                    // Usar credenciais do appsettings.json
                    services.AddSingleton<IAmazonSQS>(provider =>
                    {
                        var awsConfig = context.Configuration.GetSection("AWS").Get<AwsConfiguration>();
                        if (awsConfig == null)
                        {
                            throw new InvalidOperationException("Configuração AWS não encontrada");
                        }
                        
                        var config = new AmazonSQSConfig
                        {
                            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsConfig.Region)
                        };
                        
                        return new AmazonSQSClient(awsConfig.AccessKey, awsConfig.SecretKey, config);
                    });
                }

                // Serviços - Singleton para compatibilidade com IHostedService
                services.AddSingleton<IDynamoDbService, DynamoDbService>();
                services.AddSingleton<ISqsService, SqsService>();
                services.AddSingleton<IResilientSqsService, ResilientSqsService>();
                services.AddSingleton<IConsultaService, ConsultaService>();
                services.AddSingleton<IVerificacaoProcessorService, VerificacaoProcessorService>();
                services.AddSingleton<IExecucaoProcessorService, ProcessorService>();
                // IExecucaoProcessoService removido - CONSOLIDADO
                services.AddSingleton<IProcessoProcessorService, ProcessoProcessorService>();
                services.AddSingleton<IQueryExecutionProcessorService, QueryExecutionProcessorService>();
                
                // Novos serviços especializados
                services.AddSingleton<IQueueHealthService, QueueHealthService>();
                services.AddSingleton<IQueueManagerService, QueueManagerService>();
                services.AddSingleton<IMessageProcessorService, MessageProcessorService>();

                // Worker
                services.AddHostedService<Worker>();
            })
            .Build();

        // Log de inicialização
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        var awsConfig = host.Services.GetRequiredService<IOptions<AwsConfiguration>>();
        var sqsConfig = host.Services.GetRequiredService<IOptions<SqsConfiguration>>();
        var dynamoConfig = host.Services.GetRequiredService<IOptions<DynamoDbConfiguration>>();

        logger.LogInformation("=== Embaiadas WorkManager - Inicializando ===");
        logger.LogInformation("Região AWS: {Region}", awsConfig.Value.Region);
        logger.LogInformation("Fila Execução: {FilaExecucao}", sqsConfig.Value.FilaExecucao);
        logger.LogInformation("Fila Execução Query: {FilaExecucaoQuery}", sqsConfig.Value.FilaExecucaoQuery);
        logger.LogInformation("Fila Execução Processo: {FilaExecucaoProcesso}", sqsConfig.Value.FilaExecucaoProcesso);
        logger.LogInformation("Tabela Execução: {TableName}", dynamoConfig.Value.TableNameExecucao);
        logger.LogInformation("Tabela Verificação: {TableName}", dynamoConfig.Value.TableNameVerificacao);
        logger.LogInformation("Tabela Execução Verificação: {TableName}", dynamoConfig.Value.TableNameExecucaoVerificacao);
        // Tabela Execução Processo removida - CONSOLIDADA
        logger.LogInformation("=== Configuração concluída ===");

        host.Run();
    }
}
