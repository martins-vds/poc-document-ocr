# Review Page UX Enhancements

This document describes the user experience improvements made to the Review page for better document validation.

## Overview

The Review page has been enhanced to provide reviewers with more context about the quality of extracted data, making it easier to identify fields that need attention and verify the accuracy of OCR results. The page now features a **tabbed interface** that organizes fields by page, making it easier to review multi-page documents.

## Key Features

### 1. Tabbed Interface for Multi-Page Documents (NEW)

Documents with multiple pages are now displayed with a **tabbed interface**, where each tab represents a page from the original document. This organization makes it much easier to review and edit fields page-by-page.

**Features:**
- **Page Tabs**: Each page gets its own tab (e.g., "Page 1", "Page 2", "Page 3")
- **Organized Fields**: Fields are grouped by the page they were extracted from
- **Easy Navigation**: Click on any tab to switch between pages
- **Visual Consistency**: The first page tab is active by default
- **Bootstrap Integration**: Uses standard Bootstrap 5 tabs for familiarity

**Screenshots:**

![Page 1 Tab](https://github.com/user-attachments/assets/fdaea2b1-6453-497c-b3d5-ff1a98e11193)
*Figure 1: Review page showing Page 1 with extracted fields like fileTkNumber, accusedName, etc.*

![Page 2 Tab](https://github.com/user-attachments/assets/3070f44d-e0d2-4967-875f-a2a754c4f031)
*Figure 2: Review page showing Page 2 with fields like criminalCodeForm, policeFileNumber, agency*

![Page 3 Tab](https://github.com/user-attachments/assets/62cc151d-4288-41ec-aa24-37f809ff11d3)
*Figure 3: Review page showing Page 3 with fields like mainCharge, signatures, and dates*

### 2. Confidence Level Display

Each extracted field now displays its confidence level as a percentage badge next to the field label. The confidence level is provided by Azure Document Intelligence and indicates how confident the OCR engine is about the accuracy of the extracted value.

**Visual Indicators:**
- **90%+ confidence**: Green badge (`bg-success`) - High confidence
- **70-89% confidence**: Blue badge (`bg-info`) - Medium-high confidence
- **50-69% confidence**: Yellow badge (`bg-warning`) - Medium confidence
- **Below 50% confidence**: Red badge (`bg-danger`) - Low confidence

### 3. Field Border Color Coding

Input fields are color-coded based on their confidence level, providing an at-a-glance visual indicator:
- **Green border**: High confidence (90%+)
- **Blue border**: Medium-high confidence (70-89%)
- **Yellow border**: Medium confidence (50-69%)
- **Red border**: Low confidence (<50%)

### 4. Low Confidence Warnings

Fields with confidence levels below 70% display a warning message below the input field:
> ⚠️ Low confidence - please verify carefully

This ensures reviewers pay extra attention to fields that may contain errors.

### 5. Field Type Display

Each field shows its data type (e.g., String, Date, PhoneNumber, Double) in a subtle badge on the right side of the input field. This helps reviewers understand what type of data is expected and validate it accordingly.

### 6. Improved Form Layout

The form layout has been enhanced with:
- **Bold field labels**: Makes field names more prominent
- **Placeholder text**: Guides reviewers with "Enter or correct the value"
- **Help text**: A subtitle explains the purpose of confidence levels
- **Better spacing**: Improved visual hierarchy and readability

## User Interface Details

### Field Structure

Each field in the review form now displays:

```
┌─────────────────────────────────────────────────────────────┐
│ FieldName                            [95.2% confidence]      │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ Extracted value here                  │ [Field Type]    │ │
│ └─────────────────────────────────────────────────────────┘ │
│ ⚠️ Low confidence - please verify carefully (if < 70%)      │
└─────────────────────────────────────────────────────────────┘
```

### Color Scheme

The confidence-based color scheme follows Bootstrap's standard colors:
- **Green** (Success): Highly accurate, minimal review needed
- **Blue** (Info): Good accuracy, standard verification recommended
- **Yellow** (Warning): Moderate accuracy, careful review needed
- **Red** (Danger): Low accuracy, thorough verification required

## Technical Implementation

### Page Data Structure

The Review page now parses the `Pages` array from the document's `ExtractedData`. Each page contains:

```json
{
  "extractedData": {
    "PageCount": 3,
    "Pages": [
      {
        "PageCount": 1,
        "Fields": {
          "fileTkNumber": {
            "type": "String",
            "confidence": 0.953,
            "content": "TK-2024-001",
            "valueString": "TK-2024-001"
          },
          "accusedName": {
            "type": "String",
            "confidence": 0.652,
            "content": "John Smith",
            "valueString": "John Smith"
          }
        }
      },
      {
        "PageCount": 2,
        "Fields": {
          "policeFileNumber": {
            "type": "String",
            "confidence": 0.948,
            "content": "PF-2024-456789",
            "valueString": "PF-2024-456789"
          }
        }
      }
    ]
  }
}
```

### New Helper Methods

The following methods were added to the Review page component:

1. **`GetFieldConfidence(object value)`**: Extracts confidence level from field data
2. **`GetFieldType(object value)`**: Retrieves the field type from field data
3. **`GetConfidenceBadgeClass(float? confidence)`**: Determines badge color class based on confidence
4. **`GetFieldBorderClass(float? confidence)`**: Determines input border color class based on confidence
5. **`UpdatePageFieldValue(int pageIndex, string fieldKey, string? newValue)`**: Updates field values in page-specific data
6. **`SyncPageDataToEditedData()`**: Synchronizes changes from page tabs back to the main data structure

### Page Data Class

A new `PageData` class was added to represent each page:

```csharp
private class PageData
{
    public int PageNumber { get; set; }
    public Dictionary<string, object> Fields { get; set; } = new();
}
```

### Data Structure

The extracted data from Azure Document Intelligence is stored in the following structure:

```json
{
  "FieldName": {
    "type": "String",
    "confidence": 0.952,
    "content": "Extracted text value",
    "valueString": "Extracted text value"
  }
}
```

## Benefits for Reviewers

1. **Better Organization**: Multi-page documents are now easy to navigate with tabs
2. **Prioritization**: Quickly identify fields that need the most attention
3. **Confidence**: Visual feedback about data quality increases trust in the system
4. **Efficiency**: Spend more time on uncertain fields, less on high-confidence ones
5. **Context**: Understanding field types helps validate data appropriately
6. **Better UX**: Clear visual hierarchy and intuitive design reduces cognitive load
7. **Page-by-Page Review**: Focus on one page at a time without distraction

## Dependencies

- **Bootstrap 5.3.0**: For styling and layout
- **Bootstrap Icons 1.11.0**: For confidence and warning icons
- **Blazor Server**: For interactive server-side rendering

## Browser Compatibility

The enhanced UI uses standard Bootstrap components and should work on all modern browsers:
- Chrome/Edge (latest)
- Firefox (latest)
- Safari (latest)

## Future Enhancements

Potential improvements for future iterations:
1. ~~Tabbed interface for multi-page documents~~ ✅ **Completed**
2. Sortable fields by confidence level
3. Filter to show only low-confidence fields
4. Bulk edit mode for similar fields
5. Confidence history tracking
6. AI-assisted suggestions for corrections
7. Keyboard shortcuts for faster navigation
8. Page thumbnails in tabs for visual reference
