# Extract notification info
dumpsys notification --noredact | sed -n '/Notification List:/,/Snoozed notifications:/p' | awk 'BEGIN {
    # Use "NotificationRecord" to break records. Skip first (header) record.
    RS="NotificationRecord";
    ORS="";
    first = 1;
    print "["
}
# Function: extract_value(record, field, mode)
#   - record: full text in which to search.
#   - field: the token, e.g. "opPkg"
#   - mode: if set to "paren" then assume format like:
#           android.title=String (Tap to control SoundMaster)
#         and extract the string inside parentheses.
function extract_value(record, field, mode,    pos, val, paren_start, paren_end, arr) {
    pos = index(record, field"=");
    if (pos == 0) return "";
    # Extract everything after "field="
    val = substr(record, pos + length(field "="));
    if (mode == "paren") {
       # Find the first parenthesis (if present)
       paren_start = index(val, "(");
       if (paren_start > 0) {
           val = substr(val, paren_start+1);
           paren_end = index(val, ")");
           if (paren_end > 0)
               return substr(val, 1, paren_end-1);
           else
               return val;
       } else {
           return val;
       }
    } else {
       # Remove newline characters to get a single-line token.
       gsub(/\n/, " ", val);
       split(val, arr, " ");
       return arr[1];
    }
}
{
    if (NR > 1) {
      opPkg = extract_value($0, "opPkg");
      icon = extract_value($0, "icon");
      key = extract_value($0, "key");
      when_val = extract_value($0, "when");
      title = extract_value($0, "android.title", "paren");
      subText = extract_value($0, "android.subText","paren");
      text_val = extract_value($0, "android.text","paren");
      progress = extract_value($0, "android.progress", "paren");
      progressMax = extract_value($0, "android.progressMax", "paren");
      
      if (!first) {
          printf(",");
      }
      first = 0;
      
      # Print JSON object for the current record.
      printf("{\"opPkg\":\"%s\",\"icon\":\"%s\",\"key\":\"%s\",\"when\":\"%s\",\"android.title\":\"%s\",\"android.subText\":\"%s\",\"android.text\":\"%s\",\"android.progress\":\"%s\",\"android.progressMax\":\"%s\"}",
             opPkg, icon, key, when_val, title, subText, text_val, progress, progressMax);
    }
}
END { print "]" }'
