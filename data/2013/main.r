install.packages('readxl')
install.packages("RPostgreSQL")

library(RPostgreSQL)
library(readxl)

ec_master <- read_excel(file.path(getwd(), '/data/2013', 'ec_master_list_q2_2013_published_open_ordinary_school.xlsx' ))

ec_master2 <- read_excel("ec_masterlist_q1_2013_published_ordinary_schools.xlsx")

a  <-

demo()

drv <- dbDriver("PostgreSQL")
conn <- dbConnect(drv,
                dbname = 'matric_dashboard_db',
                 user = "wootbook_db_user",
                 password = "H2SnDHBidZUyRWSi73yctd")

cursor <- dbGetQuery(conn, '\t')

head(cursor)


dim(ec_master)

class(ec_master)

colnames(ec_master)

school_name <-  ec_master[c("Name","NatEmis")]

just_head  <- head(school_name)


dbWriteTable(conn, name = "schools", value = just_head, row.names = FALSE, overwrite = FALSE)

subset(ec_master, select = c("Name", "Sector", "Educator_Numbers_2007"))[

subset(ec_master, select = c("Name", "OwnerBuild", "Snap_Learners_2010"))[10:20,]



unique(ec_master$Snap_Learners_2010)

unique(ec_master$OwnerBuild)



length(ec_master[["Educator_Numbers_2007"]])

sum(is.na(ec_master[["Educator_Numbers_2007"]]))

ec_master.info
