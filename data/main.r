
library(RPostgreSQL)

drv <- dbDriver("PostgreSQL")
conn <- dbConnect(
    drv,
    dbname = 'matric_dashboard_db',
    user = "wootbook_db_user",
    password = "H2SnDHBidZUyRWSi73yctd"
)

school_report_2021_2023  <-  read.csv(file.path(getwd(), 'data/og-data', 'school_report_2021_2023.csv'))


tail(school_report_2021_2023)

count(school_report_2021_2023)

dim(school_report_2021_2023)

colnames(school_report_2021_2023[c("centre_name")])

school_report_2021_2023[c("centre_name")]
tail(school_report_2021_2023[c("centre_name")])



dbWriteTable(conn, name = "school_report_2021_2023", value = school_report_2021_2023, row.names = FALSE, overwrite = FALSE)
