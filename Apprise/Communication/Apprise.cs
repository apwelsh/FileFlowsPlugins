﻿using System.ComponentModel;
using System.Text.Json;

namespace FileFlows.Apprise.Communication;

/// <summary>
/// Flow element that send a notification via apprise
/// </summary>
public class Apprise: Node
{
    public override int Inputs => 1;
    /// <inheritdoc />
    public override int Outputs => 2;
    /// <inheritdoc />
    public override FlowElementType Type => FlowElementType.Communication;
    /// <inheritdoc /> 
    public override string Icon => "svg:apprise";
    /// <inheritdoc />
    public override bool FailureNode => true;
    /// <inheritdoc />
    public override string HelpUrl => "https://fileflows.com/docs/plugins/apprise/apprise";
    /// <inheritdoc />
    public override string CustomColor => "#257575";

    [StringArray(1)]
    public string[] Tag { get; set; } = new string[] { };

    [DefaultValue("info")]
    [Select(nameof(MessageTypeOptions), 2)]
    public string MessageType { get; set; } = string.Empty;

    private static List<ListOption>? _MessageTypeOptions;
    public static List<ListOption> MessageTypeOptions
    {
        get
        {
            if (_MessageTypeOptions == null)
            {
                _MessageTypeOptions = new List<ListOption>
                    {
                        new () { Label = "Information", Value = "info"},
                        new () { Label = "Success", Value = "success"},
                        new () { Label = "Warning", Value = "warning" },
                        new () { Label = "Failure", Value = "failure"}
                    };
            }
            return _MessageTypeOptions;
        }
    }


    /// <summary>
    /// Gets or sets the message
    /// </summary>
    [Required]
    [Template(3, nameof(MessageTemplates))]
    public string Message { get; set; } = string.Empty;

    private static List<ListOption>? _MessageTemplates;
    public static List<ListOption> MessageTemplates
    {
        get
        {
            if (_MessageTemplates == null)
            {
                _MessageTemplates = new List<ListOption>
                {
                    new () { Label = "Basic", Value = @"File: {{ file.Orig.FullName }}
Size: {{ file.Size }}" },
                    new () { Label = "File Size Changes", Value = @"
{{ difference = file.Size - file.Orig.Size }}
{{ percent = (difference / file.Orig.Size) * 100 | math.round 2 }}

Input File: {{ file.Orig.FullName }}
Output File: {{ file.FullName }}
Original Size: {{ file.Orig.Size | file_size }}
Final Size: {{ file.Size | file_size }}

{{- if difference > 0 }}
File grew in size: {{ difference | math.abs | file_size }}
{{ else }}
File shrunk in size by: {{ difference | file_size }} / {{ percent }}%
{{ end }}"}
                };
            }
            return _MessageTemplates;
        }
    }

    internal IAppriseApi? Api;
    
    /// <inheritdoc />
    public override int Execute(NodeParameters args)
    {
        try
        {
            if (Api == null)
            {
                var settings = args.GetPluginSettings<PluginSettings>();

                var endpoint = settings?.Endpoint;
                var serverUrl = settings?.ServerUrl;

                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    args.Logger?.WLog("No endpoint set");
                    return 2;
                }

                if (string.IsNullOrWhiteSpace(serverUrl))
                {
                    args.Logger?.WLog("No server URL set");
                    return 2;
                }

                string url = serverUrl;
                if (url.EndsWith('/') == false)
                    url += "/";
                if (endpoint.EndsWith('/'))
                    url += endpoint[1..];
                else
                    url += endpoint;
                Api = new AppriseApi(url);
            }

            string message = args.RenderTemplate!(this.Message);
            if (string.IsNullOrWhiteSpace(message))
            {
                args.Logger?.WLog("No message to send");
                return 2;
            }

            var result = Api.Send(args.Logger!, MessageType, Tag, message);

            return result ? 1 : 2;

        }
        catch (Exception ex)
        {
            args.Logger?.WLog("Error sending message: " + ex.Message);
            return 2;
        }
    }
}
