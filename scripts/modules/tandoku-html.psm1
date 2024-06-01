function AddMetaToXHtmlFile ([string]$Path, $Meta) {
    if ($Meta.Count -eq 0) {
        return
    }

    $Path = Resolve-Path $Path

    $doc = [xml](Get-Content $Path)
    $head = $doc.html.head

    foreach ($name in $Meta.Keys) {
        $value = $Meta[$name]

        $el = $doc.CreateElement('meta', $doc.html.xmlns)
        $el.SetAttribute('name', $name)
        $el.SetAttribute('content', $value)
        [void] $head.AppendChild($el)
    }

    $doc.Save($Path)
}