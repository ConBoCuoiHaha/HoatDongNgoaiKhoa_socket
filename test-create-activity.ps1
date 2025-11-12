# Test create activity
$baseUrl = "http://localhost:5109"

Write-Host "=== TEST CREATE ACTIVITY ===" -ForegroundColor Green

# Login as Teacher
$teacherLoginData = @{
    email = "teacher@edu.com"
    password = "Teacher123!"
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method POST -Body $teacherLoginData -ContentType "application/json"
    $teacherToken = $response.token
    Write-Host "Teacher login successful" -ForegroundColor Green
    
    $teacherHeaders = @{
        "Authorization" = "Bearer $teacherToken"
        "Content-Type" = "application/json"
    }
    
    # Create activity
    Write-Host "Creating activity..." -ForegroundColor Yellow
    $activityData = @{
        title = "Test Activity"
        description = "Test activity description"
        category = "Test"
        location = "Test Location"
        startTime = "2025-12-15T14:00:00"
        endTime = "2025-12-15T17:00:00"
        maxParticipants = 10
        requireApproval = $true
    } | ConvertTo-Json
    
    $activityResponse = Invoke-RestMethod -Uri "$baseUrl/api/activities" -Method POST -Body $activityData -Headers $teacherHeaders
    Write-Host "Activity created successfully!" -ForegroundColor Green
    Write-Host "Activity ID: $($activityResponse.id)" -ForegroundColor Gray
    Write-Host "Activity Title: $($activityResponse.title)" -ForegroundColor Gray
    
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Response: $responseBody" -ForegroundColor Red
    }
    Write-Host "Request data: $activityData" -ForegroundColor Yellow
}

Write-Host "Test completed!" -ForegroundColor Green 