content query --uri content://sms/inbox | awk -v max="$smslimit" '
  BEGIN {
    print "["; count=0; 
  }
  /^Row:/ {
    if (count++ >= max) {
      print "]"; exit      # triggers SIGPIPE upstream
    }
    line = $0

    # address
    a1 = index(line, "address=")
    t  = substr(line, a1+8)
    a2 = index(t, ",")
    addr = substr(t, 1, a2-1)

    # date
    d1 = index(line, "date=")
    t  = substr(line, d1+5)
    d2 = index(t, ",")
    dt   = substr(t, 1, d2-1)

    # body
    b1   = index(line, "body=")
    t    = substr(line, b1+5)
    b2   = index(t, ", service_center=")
    body = substr(t, 1, b2-1)

    # JSON-escape
    gsub(/\\/, "\\\\", body)
    gsub(/"/, "\\\"", body)

    # emit
    printf "  {\"address\":\"%s\",\"date\":\"%s\",\"body\":\"%s\"}%s\n", \
           addr, dt, body, (count<max ? "," : "")
  }
  END {
    if (count<max) print "]"
  }
'
