param([int]$Port = 8443)

$ErrorActionPreference = 'Stop'
$rsa = [System.Security.Cryptography.RSA]::Create(2048)
$request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
    'CN=localhost', $rsa,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
$names = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
$names.AddDnsName('localhost')
$request.CertificateExtensions.Add($names.Build())
$certificate = $request.CreateSelfSigned(
    [DateTimeOffset]::UtcNow.AddMinutes(-5),
    [DateTimeOffset]::UtcNow.AddHours(4))
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
$listener.Start()
Write-Host "Synthetic Application Foundry HTTPS fixture listening at https://localhost:$Port"

try {
    while ($true) {
        $client = $listener.AcceptTcpClient()
        try {
            $stream = [System.Net.Security.SslStream]::new($client.GetStream(), $false)
            $stream.AuthenticateAsServer($certificate)
            $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::ASCII, $false, 1024, $true)
            $requestLine = $reader.ReadLine()
            while (($line = $reader.ReadLine()) -ne '') {
                if ($null -eq $line) { break }
            }
            $route = ($requestLine -split ' ')[1]
            if ($requestLine.StartsWith('GET ') -and $route -eq '/job') {
                $status = '200 OK'
                $body = [System.IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'job.html'))
            }
            elseif ($requestLine.StartsWith('GET ') -and $route -eq '/apply') {
                $status = '200 OK'
                $body = [System.IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'apply.html'))
            }
            else {
                $status = '404 Not Found'
                $body = [System.Text.Encoding]::UTF8.GetBytes('Synthetic fixture route unavailable')
            }
            $headers = "HTTP/1.1 $status`r`nContent-Type: text/html; charset=utf-8`r`nContent-Length: $($body.Length)`r`nConnection: close`r`n`r`n"
            $headerBytes = [System.Text.Encoding]::ASCII.GetBytes($headers)
            $stream.Write($headerBytes, 0, $headerBytes.Length)
            $stream.Write($body, 0, $body.Length)
            $stream.Flush()
        }
        catch [System.Security.Authentication.AuthenticationException] {
            # The test browser may reject the temporary self-signed certificate.
        }
        finally {
            if ($null -ne $reader) { $reader.Dispose(); $reader = $null }
            if ($null -ne $stream) { $stream.Dispose(); $stream = $null }
            $client.Dispose()
        }
    }
}
finally {
    $listener.Stop()
    $certificate.Dispose()
    $rsa.Dispose()
}
