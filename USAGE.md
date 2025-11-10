# Unicode Spoof Guard - OutSystems Usage Guide

This guide provides step-by-step instructions and examples for using the Unicode Spoof Guard external library in your OutSystems Developer Cloud (ODC) applications.

## 1. Setup

First, ensure you have packaged the library by running the `generate_upload_package.ps1` script, which creates the `ExternalLibrary.zip` file.

1.  In the ODC Portal, navigate to the **External Logic** section.
2.  Click **Create external library**.
3.  Upload the generated `ExternalLibrary.zip` file.
4.  Once the library is created, navigate to ODC Studio.
5.  Open your application and click **Manage dependencies** (or press `Ctrl+Q`).
6.  Find **UnicodeSpoofGuard** in the list of external libraries and add a dependency to it.

You can now use the server actions in your application's logic.

## 2. Server Actions and Usage

### IsSafeEmail / IsSafeDomain / IsSafeUsername

These actions are your primary tool for validating user input. They perform a full analysis and return a simple `IsSafe` boolean along with a detailed report.

**Use Case**: Validate user input from a form before saving it to the database.

**How to Use**:
1.  In a server action or data action, drag the user-provided input (e.g., `Email` from a form).
2.  Add the `IsSafeEmail` server action from the **UnicodeSpoofGuard** library.
3.  Pass the user's email to the `email` input parameter.
4.  Use an `If` node to check the `IsSafe` property of the `AnalysisResult` output.

**Example Logic**:
```
If (IsSafeEmail.Result.IsSafe)
  // True: Proceed with logic, e.g., create the user account.
  CreateUser(Name, IsSafeEmail.Result.CanonicalValue) // Use the canonical value for storage
Else
  // False: Handle the unsafe input.
  LogMessage("Spoofing attempt detected: " + IsSafeEmail.Result.Reason)
  Feedback_Message("The email address contains suspicious characters. Please correct it and try again.")
```

#### Success Scenario
-   **Input**: `support@example.com`
-   **Output `IsSafe`**: `True`
-   **Action**: The `If` node evaluates to true, and the user is created.

#### Failure Scenario
-   **Input**: `"billing@pаypal.com"` (contains a Cyrillic 'а')
-   **Output `IsSafe`**: `False`
-   **Output `Reason`**: `"Character 'а' at position 11 is a homoglyph of 'a'."`
-   **Action**: The `If` node evaluates to false. A log message is created, and the user receives a feedback message. You can inspect the `Findings` list for more details.

---

### GetCanonicalString

This action normalizes a string by replacing confusable characters with their safe, single-character Latin equivalents.

**Use Case**: Create a "searchable" version of a username or identifier to prevent duplicates from visually similar names.

**How to Use**:
1.  Call `GetCanonicalString` with the user-provided text.
2.  Store the output in a separate, non-indexed attribute in your database (e.g., `UsernameCanonical`).
3.  When a new user registers, generate the canonical string for their desired username and check if it already exists in the `UsernameCanonical` column.

**Example Logic**:
```
// On User Registration
NewUser.Username = Form.Username
NewUser.UsernameCanonical = UnicodeSpoofGuard.GetCanonicalString(Form.Username)

// Check for duplicates
ExistingUser = Aggregate: Get User where UsernameCanonical = NewUser.UsernameCanonical

If (ExistingUser is not empty)
  // Handle duplicate
Else
  // Create new user
```

#### Success Scenario
-   **Input**: `"Pаypal"` (with Cyrillic 'а')
-   **Output**: `"Paypal"`

#### Failure (No-op) Scenario
-   **Input**: `"support"` (no confusable characters)
-   **Output**: `"support"`

---

### AnalyzeTextForSpoofing

This action provides the most detailed analysis, returning a list of every suspicious finding without making a judgment on whether the string is "safe".

**Use Case**: Implement custom, fine-grained validation rules or a security dashboard where you want to show exactly what was flagged.

**How to Use**:
1.  Call `AnalyzeTextForSpoofing` with any text input.
2.  Use a `ForEach` loop to iterate over the output list of `DetailedFinding` structures.
3.  Inside the loop, you can inspect the `FindingType`, `Description`, and `Position` of each issue to build your custom logic.

**Example Logic**:
```
Findings = UnicodeSpoofGuard.AnalyzeTextForSpoofing(Comment.Text)

ForEach (Findings)
  If (Findings.Current.FindingType = "InvisibleCharacter")
    // Block the comment or flag for moderation
  Else if (Findings.Current.FindingType = "MixedScript")
    // Add a "contains mixed scripts" tag to the comment
```

#### Success (No Findings) Scenario
-   **Input**: `"Login screen"`
-   **Output**: An empty list.

#### Multi-Finding Scenario
-   **Input**: `"admіnα"` (Cyrillic `і`, Greek `α`)
-   **Output**: A list containing two `DetailedFinding` structures:
    1.  A `Homoglyph` finding for the Cyrillic `і`.
    2.  A `MixedScript` finding reporting the mix of Latin and Greek scripts.
```
