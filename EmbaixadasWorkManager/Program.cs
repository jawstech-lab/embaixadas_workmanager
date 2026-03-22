using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SQS;
using EmbaixadasWorkManager.Configuration;
using EmbaixadasWorkManager.Interfaces;
using EmbaixadasWorkManager.Services;
using EmbaixadasWorkManager.Middleware;
using EmbaixadasWorkManager.Logging;
using Microsoft.Extensions.Options;

namespace EmbaixadasWorkManager;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configurar serviços
        builder.Services.ConfigureServices(builder.Configuration);

        var app = builder.Build();

        // Configurar pipeline
        app.ConfigurePipeline();

        app.Run();
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configurações
        services.Configure<AwsConfiguration>(
            configuration.GetSection(AwsConfiguration.SectionName));
        services.Configure<SqsConfiguration>(
            configuration.GetSection(SqsConfiguration.SectionName));
        services.Configure<DynamoDbConfiguration>(
            configuration.GetSection(DynamoDbConfiguration.SectionName));
        services.Configure<ProcessamentoConfiguration>(
            configuration.GetSection(ProcessamentoConfiguration.SectionName));
        services.Configure<PostProcessingConfiguration>(
            configuration.GetSection(PostProcessingConfiguration.SectionName));

        // Registrar configurações como singletons para injeção direta
        services.AddSingleton<SqsConfiguration>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<SqsConfiguration>>();
            return options.Value;
        });

        services.AddSingleton<DynamoDbConfiguration>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<DynamoDbConfiguration>>();
            return options.Value;
        });

        services.AddSingleton<ProcessamentoConfiguration>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<ProcessamentoConfiguration>>();
            return options.Value;
        });

        // AWS Services
        var awsConfig = configuration.GetSection(AwsConfiguration.SectionName).Get<AwsConfiguration>();
        if (awsConfig == null)
        {
            throw new InvalidOperationException("Configuração AWS não encontrada");
        }

        // DynamoDB
        if (awsConfig.UseLocalStack)
        {
            services.AddSingleton<IAmazonDynamoDB>(provider =>
            {
                var dynamoUrl = Environment.GetEnvironmentVariable("AWS_DYNAMODB_URL") ?? awsConfig.ServiceUrl;
                var config = new AmazonDynamoDBConfig
                {
                    ServiceURL = dynamoUrl,
                    UseHttp = true
                };
                var access = string.IsNullOrEmpty(awsConfig.AccessKey) ? "dummy" : awsConfig.AccessKey;
                var secret = string.IsNullOrEmpty(awsConfig.SecretKey) ? "dummy" : awsConfig.SecretKey;
                return new AmazonDynamoDBClient(access, secret, config);
            });
        }
        else
        {
            services.AddSingleton<IAmazonDynamoDB>(provider =>
            {
                var config = new AmazonDynamoDBConfig
                {
                    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsConfig.Region)
                };

                if (!string.IsNullOrEmpty(awsConfig.AccessKey) && !string.IsNullOrEmpty(awsConfig.SecretKey))
                {
                    return new AmazonDynamoDBClient(awsConfig.AccessKey, awsConfig.SecretKey, config);
                }
                
                return new AmazonDynamoDBClient(config);
            });
        }

        // DynamoDB Context
        services.AddSingleton<IDynamoDBContext>(provider =>
        {
            var client = provider.GetRequiredService<IAmazonDynamoDB>();
            return new DynamoDBContext(client);
        });

        // SQS
        if (awsConfig.UseLocalStack)
        {
            services.AddSingleton<IAmazonSQS>(provider =>
            {
                var config = new AmazonSQSConfig
                {
                    ServiceURL = awsConfig.ServiceUrl,
                    UseHttp = true
                };
                var access = string.IsNullOrEmpty(awsConfig.AccessKey) ? "dummy" : awsConfig.AccessKey;
                var secret = string.IsNullOrEmpty(awsConfig.SecretKey) ? "dummy" : awsConfig.SecretKey;
                return new AmazonSQSClient(access, secret, config);
            });
        }
        else
        {
            services.AddSingleton<IAmazonSQS>(provider =>
            {
                var config = new AmazonSQSConfig
                {
                    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsConfig.Region)
                };

                if (!string.IsNullOrEmpty(awsConfig.AccessKey) && !string.IsNullOrEmpty(awsConfig.SecretKey))
                {
                    return new AmazonSQSClient(awsConfig.AccessKey, awsConfig.SecretKey, config);
                }

                return new AmazonSQSClient(config);
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
        
        // Serviços de performance e auditoria
        services.AddSingleton<IExecucaoEmpresaService, ExecucaoEmpresaService>();
        services.AddSingleton<IDatabaseCountService, DatabaseCountService>();
        
        // Serviços de pós-processamento
        services.AddSingleton<IPostProcessingPipeline, EmbaixadasWorkManager.Services.PostProcessing.PostProcessingPipeline>();
        services.AddSingleton<IPostProcessingStep, EmbaixadasWorkManager.Services.PostProcessing.Steps.AgrupamentoStep>();
        services.AddSingleton<IPostProcessingStep, EmbaixadasWorkManager.Services.PostProcessing.Steps.ExclusaoRegistrosStep>();
        services.AddSingleton<IPostProcessingStep, EmbaixadasWorkManager.Services.PostProcessing.Steps.ProcessamentoJustificativasStep>();
        services.AddSingleton<IPostProcessingStep, EmbaixadasWorkManager.Services.PostProcessing.Steps.AgregacaoResultadosStep>();
        
        // Novos serviços especializados
        services.AddSingleton<IQueueHealthService, QueueHealthService>();
        services.AddSingleton<IQueueManagerService, QueueManagerService>();
        services.AddSingleton<IMessageProcessorService, MessageProcessorService>();

        // Serviços de Log
        services.AddSingleton<ILogService, LogService>();

        // Configurar logging personalizado
        services.AddLogging(builder =>
        {
            // O provider será adicionado após a construção do app
        });

        // Controllers
        services.AddControllers();

        // Worker
        services.AddHostedService<Worker>();

        return services;
    }
}

public static class WebApplicationExtensions
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        // Middleware de captura de logs
        app.UseMiddleware<LogCaptureMiddleware>();

        // Configurar logging personalizado
        var logService = app.Services.GetRequiredService<ILogService>();
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        loggerFactory.AddProvider(new InMemoryLoggerProvider(logService));

        // Mapear controllers
        app.MapControllers();

        return app;
    }
}
