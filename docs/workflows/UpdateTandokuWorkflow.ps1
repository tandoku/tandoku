# prereqs:
# scoop install graphviz
# install-module psgraph

param(
    [Parameter(ValueFromPipeline=$true)]
    [String[]]
    $Path = '.\*.workflow.yaml',

    [Parameter()]
    [String]
    $OutFile,

    [Parameter()]
    [Switch]
    $ShowGraph
)

$colors = @{
    text = 'white'
    artifacts = '#898d90'
    operations = '#4e6a94'
    values = '#767064'
}
$fontName = 'Franklin Gothic Book'

function BuildTandokuWorkflowGraph($wfDocs) {
    graph workflow @{fontname=$fontName} {
        node @{fontname=$fontName; fontcolor=$colors.text; penwidth="0.2"}
        edge @{fontname=$fontName; arrowsize="0.6"}

        foreach ($wf in $wfDocs) {
            Inline "# stage: $($wf.stage), media: $($wf.media)"

            # TODO: subgraphs based on different criteria ?
            #subgraph {
                # artifacts
                if ($wf.artifacts) {
                    node @{shape = 'rect'; style = "filled,rounded"; fillcolor = $colors.artifacts }
                    AddNodesFromKeys $wf.artifacts
                }

                # values
                if ($wf.simpleValues) {
                    node @{shape = 'rect'; style = "filled,rounded"; fillcolor = $colors.values }
                    AddNodesFromKeys $wf.simpleValues
                }

                # operations
                if ($wf.operations) {
                    node @{shape = 'rect'; style = "filled"; fillcolor = $colors.operations }
                    AddNodesFromKeys $wf.operations

                    # undefined nodes (created by edges) should be values
                    # TODO: share with values above
                    node @{shape = 'rect'; style = "filled,rounded"; fillcolor = $colors.values }

                    $wf.operations.keys | ForEach-Object {
                        $o = $wf.operations[$_]
                        if ($o.inputs) {
                            edge $o.inputs $_
                        }
                        if ($o.outputs) {
                            edge $_ $o.outputs
                        }
                    }
                }
            #}
        }
    }
}

function AddNodesFromKeys($map) {
    $map.keys | ForEach-Object {
        $o = $map[$_]
        $attrs = @{}
        $attrs.label = $_
        $statusIndicator = $o.status ? (GetIndicatorFromStatus $o.status) : $null
        if ($statusIndicator) { $attrs.label = "$($attrs.label) $statusIndicator" }
        if ($o.summary) { $attrs.tooltip = $o.summary }
        node -Name $_ -Attributes $attrs
    }
}

function GetIndicatorFromStatus($status) {
    switch ($status) {
        'maybe' { '❓' } # TODO: for operations, use ❔ instead?
        'next' { '⬅️' }
        'partial' { '🚧' }
        'done' { '✅' }
        default { '' }
    }
}

function BuildTandokuArtifactsTable($wfDocs) {
    $wfDocs | ForEach-Object {
        $wf = $_

        if ($wf.artifacts) {
            foreach ($k in $wf.artifacts.keys) {
                $a = $wf.artifacts[$k]
                [PSCustomObject] @{
                    name = $k
                    container = $a.container
                    stage = $wf.stage
                    media = $wf.media
                    location = $a.location
                    sourceControl = $a.sourceControl
                    summary = $a.summary
                    notes = $a.notes
                }
            }
        }
    } | Export-Csv '.\tandoku-workflow-artifacts.csv'
}

function GetTandokuWorkflowDocs($Path) {
    return Get-ChildItem $Path |
        ForEach-Object {
            Get-Content $_ | ConvertFrom-Yaml -AllDocuments
        }
}

$wfDocs = GetTandokuWorkflowDocs $Path

$graph = BuildTandokuWorkflowGraph $wfDocs

if ($OutFile) {
    $graph | Set-Content $OutFile
}

if ($ShowGraph) {
    # TODO: add option to cleanup temp files (rm $env:temp\*.*.svg)
    $dotPath = (Get-Command dot).Source
    $graph | Show-PSGraph -GraphVizPath $dotPath -OutputFormat svg
}

if (-not $OutFile -and -not $ShowGraph) {
    $graph
}

# TODO: rationalize the parameters to allow building graph, tables or both
BuildTandokuArtifactsTable $wfDocs
